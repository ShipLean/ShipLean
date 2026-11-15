namespace ShipLean.Contracts.Inspection;

public sealed record AgentRunRowDto(
    string RunId,
    string Agent,
    string AgentLabel,
    string? StartedBy,
    string StartedLabel,
    string? WhatItDid,
    LinkSource LinkSource,
    int Turns,
    int SpanOrEventCount,
    bool LogOnly,
    string? Model,
    long TookMs,
    long? TokensIn,
    long? TokensOut,
    int Errors,
    IReadOnlyList<TimelineSegmentDto> Timeline);
