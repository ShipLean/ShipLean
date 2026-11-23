namespace ShipLean.Contracts.Analytics;

// Where accepted web vitals readings wait for the dispatcher. Track never blocks and never throws; when full it drops the sample
// and counts it on shiplean.web_vitals.dropped.
public interface IWebVitalsQueue
{
    void Track(WebVitalsSample sample);
}
