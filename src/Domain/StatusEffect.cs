// Активный эффект на персонаже или враге.
// Type: "phys_taken_pct" (Пролом брони — на цель), "magic_dmg_pct" (Маг. фокус — на игрока).
// Remaining уменьшается на 1 в конце хода игрока.

public class StatusEffect
{
    public string Id;
    public string Type;
    public float Amount;
    public int Remaining;
}

public class Intent
{
    public string Type;     // "attack" / "block"
    public int Amount;
    public string Name;
}
