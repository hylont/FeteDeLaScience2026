using System;
using UnityEngine;

[Serializable]
public class TrialData
{
    public ETrialCondition Condition;
    public EFlavor ColorFlavor;
    public EFlavor ScentFlavor;

    private static readonly EFlavor[] AllFlavors = { EFlavor.Chocolat, EFlavor.Citron, EFlavor.Fraise };

    public static TrialData GenerateCoherent()
    {
        EFlavor flavor = AllFlavors[UnityEngine.Random.Range(0, AllFlavors.Length)];
        return new TrialData
        {
            Condition = ETrialCondition.Coherent,
            ColorFlavor = flavor,
            ScentFlavor = flavor
        };
    }

    public static TrialData GenerateIncoherent()
    {
        EFlavor colorFlavor = AllFlavors[UnityEngine.Random.Range(0, AllFlavors.Length)];
        EFlavor scentFlavor;
        do
        {
            scentFlavor = AllFlavors[UnityEngine.Random.Range(0, AllFlavors.Length)];
        } while (scentFlavor == colorFlavor);

        return new TrialData
        {
            Condition = ETrialCondition.Incoherent,
            ColorFlavor = colorFlavor,
            ScentFlavor = scentFlavor
        };
    }
}
