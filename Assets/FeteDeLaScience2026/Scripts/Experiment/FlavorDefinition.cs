using System;
using UnityEngine;

[Serializable]
public class FlavorDefinition
{
    public EFlavor Flavor;
    public Color Color = Color.white;

    [Tooltip("Index of the Olfy slot (vial) that diffuses this flavor's scent.")]
    [Min(1)] public int ScentSlot = 1;

    public FlavorDefinition() { }

    public FlavorDefinition(EFlavor flavor, Color color, int scentSlot)
    {
        Flavor = flavor;
        Color = color;
        ScentSlot = scentSlot;
    }
}
