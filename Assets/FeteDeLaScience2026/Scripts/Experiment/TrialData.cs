using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TrialData
{
    public ETrialCondition Condition;
    public EFlavor ColorFlavor;
    public EFlavor ScentFlavor;

    private static readonly EFlavor[] AllFlavors = { EFlavor.Chocolat, EFlavor.Citron, EFlavor.Fraise };

    // usedColors/usedScents are the flavors already shown as a color/diffused as a scent in earlier
    // trials of the same experience, so the same color or scent is never reused across the experiment.
    public static TrialData GenerateCoherent(ICollection<EFlavor> usedColors, ICollection<EFlavor> usedScents)
    {
        HashSet<EFlavor> exclude = new HashSet<EFlavor>(usedColors);
        exclude.UnionWith(usedScents);
        EFlavor flavor = PickUnusedFlavor(exclude);

        return new TrialData
        {
            Condition = ETrialCondition.Coherent,
            ColorFlavor = flavor,
            ScentFlavor = flavor
        };
    }

    public static TrialData GenerateIncoherent(ICollection<EFlavor> usedColors, ICollection<EFlavor> usedScents)
    {
        EFlavor colorFlavor = PickUnusedFlavor(usedColors);

        HashSet<EFlavor> scentExclude = new HashSet<EFlavor>(usedScents) { colorFlavor };
        EFlavor scentFlavor = PickUnusedFlavor(scentExclude);

        return new TrialData
        {
            Condition = ETrialCondition.Incoherent,
            ColorFlavor = colorFlavor,
            ScentFlavor = scentFlavor
        };
    }

    private static EFlavor PickUnusedFlavor(ICollection<EFlavor> exclude)
    {
        List<EFlavor> candidates = new List<EFlavor>();
        foreach (EFlavor flavor in AllFlavors)
        {
            if (!exclude.Contains(flavor)) candidates.Add(flavor);
        }

        if (candidates.Count == 0)
        {
            LLogger.E("No flavor left that satisfies the no-repeat constraint; reusing one at random.");
            return AllFlavors[UnityEngine.Random.Range(0, AllFlavors.Length)];
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
}
