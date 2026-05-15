public enum TeamType
{
    Neutral = 0,
    Player  = 1,
    Enemy1  = 2,
    Enemy2  = 3,
    Enemy3  = 4
}

public static class TeamHelper
{
    public static bool IsHostile(TeamType a, TeamType b)
    {
        if (a == TeamType.Neutral || b == TeamType.Neutral) return false;
        return a != b;
    }
}
