﻿namespace PulseGuard.Models;

public class PulseOptions
{
    private int _interval = 1;
    public int Interval
    {
        get => _interval;
        set => _interval = Math.Max(1, value);
    }

    private int _simultaneousPulses = 5;
    public int SimultaneousPulses
    {
        get => _simultaneousPulses;
        set => _simultaneousPulses = Math.Max(1, value);
    }

    private int _webhookDelay = 5;
    public int WebhookDelay
    {
        get => _webhookDelay;
        set => _webhookDelay = Math.Max(0, value);
    }

    public PulseStates TimeOutState { get; set; } = PulseStates.Unhealthy;

    public string Store { get; set; } = "";
}