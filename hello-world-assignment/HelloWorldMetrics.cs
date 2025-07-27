using System.Diagnostics.Metrics;

namespace hello_world_assignment;

public class HelloWorldMetrics
{
    private readonly ILogger<HelloWorldMetrics> _logger;
    
    private Counter<int> HelloWorldCounter { get; }

    public HelloWorldMetrics(ILogger<HelloWorldMetrics> logger, IMeterFactory  meterFactory)
    {
        _logger = logger;

        var lMeter = meterFactory.Create("HelloWorldApp");
        
        HelloWorldCounter = lMeter.CreateCounter<int>("hello_world_counter");
    }

    public void ButtonPressed()
    {
        _logger.LogInformation("Begin recording button pressed metrics...");
        
        _logger.LogInformation("Incrementing HelloWorldCounter");
        
        HelloWorldCounter.Add(1);
    }
}