#!/usr/bin/env python3
"""Builds ShipLean's Linear-compatible schema from the pinned @linear/sdk (SHIP-186).

Input : linear/linear packages/sdk/src/schema.graphql and _generated_documents.ts at one commit.
Output: (1) the raw documents the SDK sends for each operation ShipLean serves (Fixtures/linear-sdk/*.graphql, the
        WebApi tests post them verbatim) and (2) a trimmed schema holding exactly the types and fields those documents
        select, as a C# file (WebApi/Features/LinearApi/Schema/LinearSchemaSdl.cs; csproj edits are frozen, so the SDL
        is a raw string, not an embedded resource).

Usage: scripts/linear-schema-extract.py <dir with the two pinned files> <repo root>
No third-party packages. Re-run on a version bump, then diff.
"""
import os
import re
import sys

PIN = "618a737a4e6aa279b7853c650f79f209a5142bbc"

# SDK operation name -> fixture file. The relay's own documents live beside them, hand-written.
OPERATIONS = [
    "viewer", "issue", "issue_comments", "agentSession", "agentSession_activities", "agentActivity",
    "issueRepositorySuggestions", "createAgentActivity", "updateAgentSession", "agentSessionUpdateExternalUrl", "createComment",
]
# Extra documents the relay sends (hand-written; they must validate against the same schema).
RELAY_DOCUMENTS = {
    "relay_agentActivityCreate": """mutation agentActivityCreate($input: AgentActivityCreateInput!) {
  agentActivityCreate(input: $input) {
    success
    lastSyncId
    agentActivity { id }
  }
}""",
    "relay_agentSessionUpdate": """mutation agentSessionUpdate($id: String!, $input: AgentSessionUpdateInput!) {
  agentSessionUpdate(id: $id, input: $input) {
    success
    lastSyncId
    agentSession { id }
  }
}""",
    "relay_issueRepositorySuggestions": """query issueRepositorySuggestions($issueId: String!, $agentSessionId: String, $candidateRepositories: [CandidateRepository!]!) {
  issueRepositorySuggestions(issueId: $issueId, agentSessionId: $agentSessionId, candidateRepositories: $candidateRepositories) {
    suggestions { repositoryFullName hostname confidence }
  }
}""",
    "relay_issue": """query issue($id: String!) {
  issue(id: $id) {
    id
    identifier
    title
    description
    url
  }
}""",
}
# Input types ShipLean accepts in full (everything else reached through a `filter` argument keeps only scalar fields).
FULL_INPUTS = {
    "AgentActivityCreateInput", "AgentSessionUpdateInput", "AgentSessionUpdateExternalUrlInput", "CommentCreateInput",
    "CandidateRepository", "AgentSessionExternalLinkInput", "AgentSessionPlanItemInput", "AgentSessionUpdateExternalUrlInput",
}

TOKEN = re.compile(r'"""(?:\\"""|[^"]|"(?!""))*"""|"(?:\\.|[^"\\])*"|#[^\n]*|\.\.\.|[!$&():=@\[\]{|}]|[A-Za-z_][A-Za-z0-9_]*|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|,|\s+')


def tokenize(text):
    out = []
    for m in TOKEN.finditer(text):
        t = m.group(0)
        if t.isspace() or t == "," or t.startswith("#"):
            continue
        out.append(t)
    return out


class Parser:
    def __init__(self, tokens):
        self.t = tokens
        self.i = 0

    def peek(self, k=0):
        return self.t[self.i + k] if self.i + k < len(self.t) else None

    def next(self):
        v = self.t[self.i]
        self.i += 1
        return v

    def expect(self, v):
        n = self.next()
        assert n == v, f"expected {v} got {n} near {self.t[self.i-5:self.i+5]}"

    def desc(self):
        if self.peek() and self.peek().startswith('"'):
            self.next()

    def value(self):
        t = self.next()
        if t == "[":
            parts = []
            while self.peek() != "]":
                parts.append(self.value())
            self.next()
            return "[" + ", ".join(parts) + "]"
        if t == "{":
            parts = []
            while self.peek() != "}":
                k = self.next()
                self.expect(":")
                parts.append(f"{k}: {self.value()}")
            self.next()
            return "{" + ", ".join(parts) + "}"
        if t == "$":
            return "$" + self.next()
        return t

    def directives(self):
        while self.peek() == "@":
            self.next()
            self.next()
            if self.peek() == "(":
                self.next()
                while self.peek() != ")":
                    self.next()
                    self.expect(":")
                    self.value()
                self.next()

    def type_ref(self):
        if self.peek() == "[":
            self.next()
            inner = self.type_ref()
            self.expect("]")
            s = f"[{inner}]"
        else:
            s = self.next()
        if self.peek() == "!":
            self.next()
            s += "!"
        return s

    def args(self):
        out = []
        if self.peek() == "(":
            self.next()
            while self.peek() != ")":
                self.desc()
                name = self.next()
                self.expect(":")
                ty = self.type_ref()
                default = None
                if self.peek() == "=":
                    self.next()
                    default = self.value()
                self.directives()
                out.append((name, ty, default))
            self.next()
        return out

    def sdl(self):
        defs = {}
        while self.peek() is not None:
            self.desc()
            kw = self.next()
            if kw in ("type", "interface", "input"):
                name = self.next()
                if self.peek() == "implements":
                    self.next()
                    while self.peek() not in ("{", "@"):
                        if self.peek() == "&":
                            self.next()
                        else:
                            self.next()
                self.directives()
                fields = {}
                if self.peek() == "{":
                    self.next()
                    while self.peek() != "}":
                        self.desc()
                        fname = self.next()
                        fargs = self.args()
                        self.expect(":")
                        fty = self.type_ref()
                        default = None
                        if self.peek() == "=":
                            self.next()
                            default = self.value()
                        self.directives()
                        fields[fname] = (fargs, fty, default)
                    self.next()
                defs[name] = ("input" if kw == "input" else kw, fields)
            elif kw == "enum":
                name = self.next()
                self.directives()
                self.expect("{")
                vals = []
                while self.peek() != "}":
                    self.desc()
                    vals.append(self.next())
                    self.directives()
                self.next()
                defs[name] = ("enum", vals)
            elif kw == "union":
                name = self.next()
                self.directives()
                self.expect("=")
                if self.peek() == "|":
                    self.next()
                members = [self.next()]
                while self.peek() == "|":
                    self.next()
                    members.append(self.next())
                defs[name] = ("union", members)
            elif kw == "scalar":
                name = self.next()
                self.directives()
                defs[name] = ("scalar", None)
            elif kw == "directive":
                self.expect("@")
                self.next()
                self.args()
                if self.peek() == "repeatable":
                    self.next()
                self.expect("on")
                if self.peek() == "|":
                    self.next()
                self.next()
                while self.peek() == "|":
                    self.next()
                    self.next()
            elif kw == "schema":
                self.expect("{")
                while self.peek() != "}":
                    self.next()
                self.next()
            else:
                raise SystemExit(f"unexpected top-level {kw}")
        return defs

    # Executable documents: returns (operations, fragments). Selection = list of ("field", name, sub) | ("inline", type, sub) | ("spread", name)
    def selection_set(self):
        self.expect("{")
        sels = []
        while self.peek() != "}":
            if self.peek() == "...":
                self.next()
                if self.peek() == "on":
                    self.next()
                    ty = self.next()
                    self.directives()
                    sels.append(("inline", ty, self.selection_set()))
                else:
                    sels.append(("spread", self.next()))
                    self.directives()
                continue
            name = self.next()
            if self.peek() == ":":
                self.next()
                name = self.next()
            if self.peek() == "(":
                self.next()
                while self.peek() != ")":
                    self.next()
                    self.expect(":")
                    self.value()
                self.next()
            self.directives()
            sub = self.selection_set() if self.peek() == "{" else None
            sels.append(("field", name, sub))
        self.next()
        return sels

    def document(self):
        ops, frags = [], {}
        while self.peek() is not None:
            kw = self.next()
            if kw == "fragment":
                name = self.next()
                self.expect("on")
                ty = self.next()
                self.directives()
                frags[name] = (ty, self.selection_set())
            else:
                if self.peek() not in ("{", "("):
                    self.next()  # operation name
                if self.peek() == "(":
                    self.next()
                    while self.peek() != ")":
                        self.expect("$")
                        self.next()
                        self.expect(":")
                        self.type_ref()
                        if self.peek() == "=":
                            self.next()
                            self.value()
                        self.directives()
                    self.next()
                self.directives()
                ops.append((kw, self.selection_set()))
        return ops, frags


def base(ty):
    return ty.replace("[", "").replace("]", "").replace("!", "")


BUILTIN = {"String", "Int", "Float", "Boolean", "ID"}


def main():
    src, root = sys.argv[1], sys.argv[2]
    schema = Parser(tokenize(open(os.path.join(src, "schema.graphql"), encoding="utf-8").read())).sdl()
    ts = open(os.path.join(src, "_generated_documents.ts"), encoding="utf-8").read()
    docs = {}
    for m in re.finditer(r"export const (\w+)Document = /\*#__PURE__\*/ new TypedDocumentString\(\s*`(.*?)`\)", ts, re.S):
        text = m.group(2).strip("\n")
        head = re.match(r"\s*(query|mutation)\s+(\w+)", text)
        if head:
            docs[head.group(2)] = text
    missing = [o for o in OPERATIONS if o not in docs]
    assert not missing, f"documents not found: {missing}"

    fixtures = os.path.join(root, "src/ShipLean.WebApi.Tests/Fixtures/linear-sdk")
    os.makedirs(fixtures, exist_ok=True)
    for name in OPERATIONS:
        open(os.path.join(fixtures, f"{name}.graphql"), "w", encoding="utf-8", newline="\n").write(docs[name] + "\n")
    for name, text in RELAY_DOCUMENTS.items():
        open(os.path.join(fixtures, f"{name}.graphql"), "w", encoding="utf-8", newline="\n").write(text + "\n")
    open(os.path.join(fixtures, "README.md"), "w", encoding="utf-8", newline="\n").write(
        f"Raw operation documents from @linear/sdk (linear/linear packages/sdk/src/_generated_documents.ts at commit {PIN}), "
        "posted verbatim by the LinearApi tests. `relay_*` files are the agent-relay's own documents (hand-written). "
        "Regenerate with scripts/linear-schema-extract.py.\n")

    # Merge every selection into {type: {field: merged sub-selection map}} by walking the documents against the schema.
    needed = {}  # type name -> {field name -> True}
    used_inputs = {}  # type name -> full?
    unions = {}  # union name -> set of members used

    def frag_defs():
        allf = {}
        for text in list(docs[o] for o in OPERATIONS) + list(RELAY_DOCUMENTS.values()):
            ops, fr = Parser(tokenize(text)).document()
            allf.update(fr)
        return allf

    fragments = frag_defs()
    roots = {"query": "Query", "mutation": "Mutation"}

    def walk(type_name, sels):
        kind, body = schema[type_name]
        for s in sels:
            if s[0] == "spread":
                fty, fsels = fragments[s[1]]
                walk(fty, fsels)
            elif s[0] == "inline":
                walk(s[1], s[2])
                if kind == "union":
                    unions.setdefault(type_name, set()).add(s[1])
            else:
                _, fname, sub = s
                if fname == "__typename":
                    continue
                assert kind in ("type", "interface"), f"{type_name} is {kind}"
                needed.setdefault(type_name, {})[fname] = True
                fargs, fty, _ = body[fname]
                for _, aty, _ in fargs:
                    note_input(base(aty), full=base(aty) in FULL_INPUTS)
                if sub is not None:
                    walk(base(fty), sub)
                else:
                    if base(fty) in schema and schema[base(fty)][0] in ("type", "interface"):
                        needed.setdefault(base(fty), {})

    def note_input(name, full):
        if name in BUILTIN or name not in schema:
            return
        kind, body = schema[name]
        if kind != "input":
            used_inputs.setdefault(name, True)
            return
        if used_inputs.get(name) is True:
            return
        used_inputs[name] = full
        if full:
            for _, (_, fty, _) in body.items():
                note_input(base(fty), full=True)

    for text in list(docs[o] for o in OPERATIONS) + list(RELAY_DOCUMENTS.values()):
        # variable types
        for m in re.finditer(r"\$\w+:\s*([\[\]\w!]+)", text.split("{", 1)[0]):
            note_input(base(m.group(1)), full=base(m.group(1)) in FULL_INPUTS)
        ops, _ = Parser(tokenize(text)).document()
        for kw, sels in ops:
            walk(roots[kw], sels)

    # Inputs reached only through filters keep scalar-ish fields; resolve what they keep.
    def keep_input_fields(name):
        kind, body = schema[name]
        full = used_inputs[name] is True
        out = {}
        for fname, (_, fty, default) in body.items():
            b = base(fty)
            if full or b in BUILTIN or (b in schema and schema[b][0] in ("enum", "scalar")) or is_flat_input(b):
                out[fname] = (fty, default)
                if not full and b in schema and schema[b][0] == "input":
                    used_inputs.setdefault(b, False)
        return out

    def is_flat_input(b):
        if b not in schema or schema[b][0] != "input":
            return False
        return all(base(t) in BUILTIN or (base(t) in schema and schema[base(t)][0] in ("enum", "scalar")) for _, (_, t, _) in schema[b][1].items())

    # Emit.
    lines = []
    emitted_enums, emitted_scalars = set(), set()

    def want_type(ty):
        b = base(ty)
        if b in BUILTIN:
            return
        k = schema[b][0]
        if k == "enum":
            emitted_enums.add(b)
        elif k == "scalar":
            emitted_scalars.add(b)

    # fixpoint over inputs (nested flat inputs)
    changed = True
    input_fields = {}
    while changed:
        changed = False
        for name in list(used_inputs):
            if schema[name][0] != "input":
                continue
            kept = keep_input_fields(name)
            if input_fields.get(name) != kept:
                input_fields[name] = kept
                changed = True

    def fmt_default(d):
        return f" = {d}" if d is not None else ""

    out_types = []
    for name in sorted(needed):
        kind, body = schema[name]
        if kind == "union":
            continue
        fields = needed[name]
        if name in ("Query", "Mutation") or True:
            rows = []
            for fname in sorted(fields) if name not in ("Query", "Mutation") else list(fields):
                fargs, fty, _ = body[fname]
                want_type(fty)
                a = ""
                if fargs:
                    for _, aty, _ in fargs:
                        want_type(aty)
                    a = "(" + ", ".join(f"{n}: {t}{fmt_default(d)}" for n, t, d in fargs) + ")"
                rows.append(f"  {fname}{a}: {fty}")
            if not rows:
                rows = ["  id: ID!"] if "id" in body else [f"  {next(iter(body))}: {body[next(iter(body))][1]}"]
                want_type(body[next(iter(body))][1] if "id" not in body else "ID")
            out_types.append((kind, name, rows))
    # union members must exist as types
    for u, members in unions.items():
        for m in members:
            assert m in needed, f"{u} member {m} has no selected fields"

    for name in sorted(used_inputs):
        if schema[name][0] != "input":
            continue
        fields = input_fields[name]
        if not fields:
            continue
        rows = []
        for fname in sorted(fields):
            fty, default = fields[fname]
            want_type(fty)
            rows.append(f"  {fname}: {fty}{fmt_default(default)}")
        out_types.append(("input", name, rows))
    for name in sorted(used_inputs):
        if schema[name][0] == "input" and not input_fields[name]:
            raise SystemExit(f"input {name} lost all fields")

    sdl = []
    sdl.append("schema { query: Query mutation: Mutation }")
    for s in sorted(emitted_scalars):
        sdl.append(f"scalar {s}")
    for e in sorted(emitted_enums):
        sdl.append("enum %s {\n%s\n}" % (e, "\n".join(f"  {v}" for v in schema[e][1])))
    for u in sorted(unions):
        members = sorted(unions[u])
        sdl.append(f"union {u} = " + " | ".join(members))
    for kind, name, rows in out_types:
        sdl.append("%s %s {\n%s\n}" % (kind, name, "\n".join(rows)))
    # enums referenced only through inputs added late
    text = "\n\n".join(sdl)

    target = os.path.join(root, "src/ShipLean.WebApi/Features/LinearApi/Schema/LinearSchemaSdl.cs")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    assert '"""' not in text
    cs = f'''namespace ShipLean.WebApi.Features.LinearApi.Schema;

// GENERATED by scripts/linear-schema-extract.py from linear/linear packages/sdk/src/schema.graphql and
// _generated_documents.ts at commit {PIN} (@linear/sdk 99.0.0, master on 2026-10-28). Do not edit by hand.
// It holds exactly the types and fields the SDK's documents for the operations ShipLean serves (and the relay's own
// documents) select, with their arguments. Linear marks agent signals and plans as technology preview: re-run on a bump.
// Input types reached only through `filter` arguments keep their scalar fields; ShipLean ignores filters.
public static class LinearSchemaSdl
{{
    public const string Pin = "{PIN}";

    public const string Text = """
{text}
""";
}}
'''
    open(target, "w", encoding="utf-8", newline="\n").write(cs)
    print(f"types={len(out_types)} lines={text.count(chr(10))} bytes={len(text)}")


if __name__ == "__main__":
    main()
