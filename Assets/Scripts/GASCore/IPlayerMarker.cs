namespace UCCS
{
    /// <summary>
    /// 玩家标记接口 — 用于 AttributeSet 识别玩家身份，避免对 PlayerModel 的硬引用。
    /// PlayerModel（Assembly-CSharp）实现此接口。
    /// </summary>
    public interface IPlayerMarker { }
}
