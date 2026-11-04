namespace ShipLean.Contracts.Demos;

public interface ISprintDemoPrompt
{
    string Render(SprintDemoData data, DemoUploadTarget upload, DemoBrand brand);
}
