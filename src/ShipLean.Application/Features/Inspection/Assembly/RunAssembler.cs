using Agoda.IoC.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

[RegisterSingleton]
public sealed class RunAssembler : IRunAssembler
{
    // TODO: group spans into turns by prompt, nest rows by parent, collapse subagents, mark waits, errors and retries, join logs
    // and derive WhatItDid from ShipLean tool calls. Until then every span is one row in a single turn.
    public AssembledRun Assemble(RawRun raw)
    {
        var rows = raw.Spans
            .OrderBy(span => span.Start)
            .Select(span => new RunRow(
                span.SpanId, span.ParentSpanId, 0, RowKind.Event, span.Name, span.Start, span.End, (long)(span.End - span.Start).TotalMilliseconds,
                span.Status == SpanStatus.Error, false, null, null, 0, span.Attributes, null, null, [], null, null))
            .ToList();
        return new AssembledRun(
            raw.Summary,
            raw.Spans.Count == 0,
            [new Turn(1, raw.Summary.StartedAt, raw.Summary.EndedAt, null, null, rows)],
            [],
            null);
    }
}
