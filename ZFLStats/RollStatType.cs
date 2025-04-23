namespace ZFLStats;

public enum RollStatType
{
    /// <summary>
    /// attacker down, both down, push, push, defender stumbles, defender down
    /// </summary>
    Block,

    /// <summary>
    /// armor or injury (lower is better)
    /// </summary>
    ArmorOrInjury,

    /// <summary>
    /// D16 roll (lower is better)
    /// </summary>
    Casualty,

    /// <summary>
    /// everything else, eg dodge, pass, catch (higher is better)
    /// </summary>
    Other,
}
