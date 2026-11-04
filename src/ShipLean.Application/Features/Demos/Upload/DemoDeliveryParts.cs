using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Upload;

// Video is the unread multipart stream behind the upload's ftyp check; Token is the upload's own, so a delivery can see it was revoked.
public sealed record DemoDeliveryParts(TransferGrant Grant, string Token, DemoChaptersJson Chapters, string Script, Stream Video);
