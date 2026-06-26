using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XenoPreview
{
    [StaticConstructorOnStartup]
    public static class XenoPreview
    {
        public static XenoPreviewWindow PreviewWindowInstance;

        static XenoPreview()
        {
            try
            {
                var harmony = new Harmony("coolnether123.XenoPreview");
                int appliedPatches = 0;

                if (TryPatch(
                    harmony,
                    "Window.Close",
                    AccessTools.Method(typeof(Window), "Close", new[] { typeof(bool) }),
                    postfix: AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "Close_Postfix")))
                    appliedPatches++;

                // Patch RimWorld.GeneUtility.GenerateXenotypeNameFromGenes so it retries used names.
                if (TryPatch(
                    harmony,
                    "GeneUtility.GenerateXenotypeNameFromGenes",
                    AccessTools.Method(typeof(RimWorld.GeneUtility), "GenerateXenotypeNameFromGenes", new[] { typeof(List<GeneDef>) }),
                    prefix: AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "GenerateXenotypeNameFromGenes_Prefix")))
                    appliedPatches++;

                // Open the XenoPreview window when the Xenotype Creator or Gene Assembler is opened
                if (TryPatch(
                    harmony,
                    "GeneCreationDialogBase.PreOpen",
                    AccessTools.Method(typeof(GeneCreationDialogBase), "PreOpen", Type.EmptyTypes),
                    postfix: AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "PreOpen_Prefix")))
                    appliedPatches++;

                Log.Message("[XenoPreview] Harmony patches applied. Applied " + appliedPatches + "/3 patches.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[XenoPreview] MOD LOAD: CRITICAL - Failed to apply Harmony patches: "
                        + ex.ToString()
                );
            }
        }

        private static bool TryPatch(
            Harmony harmony,
            string patchName,
            MethodInfo original,
            MethodInfo prefix = null,
            MethodInfo postfix = null)
        {
            if (original == null)
            {
                Log.Error("[XenoPreview] Failed to find original method for patching " + patchName);
                return false;
            }

            if (prefix == null && postfix == null)
            {
                Log.Error("[XenoPreview] Failed to find patch method for " + patchName);
                return false;
            }

            try
            {
                harmony.Patch(
                    original,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Failed to apply Harmony patch for " + patchName + ": " + ex);
                return false;
            }
        }
    }

    public static class Dialog_CreateXenotype_Patches
    {
        private const int MaxXenotypeNameAttempts = 150;
        private static readonly WeightedNamePart[] CommonPrefixes =
        {
            new WeightedNamePart("xeno"),
            new WeightedNamePart("alt"),
            new WeightedNamePart("ab"),
            new WeightedNamePart("out"),
            new WeightedNamePart("sub"),
            new WeightedNamePart("exo"),
            new WeightedNamePart("tube"),
            new WeightedNamePart("lab"),
            new WeightedNamePart("pod"),
            new WeightedNamePart("an"),
            new WeightedNamePart("bon"),
            new WeightedNamePart("cel"),
            new WeightedNamePart("del"),
            new WeightedNamePart("ech"),
            new WeightedNamePart("fil"),
            new WeightedNamePart("gor"),
            new WeightedNamePart("hor"),
            new WeightedNamePart("ich"),
            new WeightedNamePart("jack"),
            new WeightedNamePart("kol"),
            new WeightedNamePart("lox"),
            new WeightedNamePart("mack"),
            new WeightedNamePart("nor"),
            new WeightedNamePart("os"),
            new WeightedNamePart("per"),
            new WeightedNamePart("quin"),
            new WeightedNamePart("rax"),
            new WeightedNamePart("sil"),
            new WeightedNamePart("tel"),
            new WeightedNamePart("ultra"),
            new WeightedNamePart("vox"),
            new WeightedNamePart("whis"),
            new WeightedNamePart("xen"),
            new WeightedNamePart("yon"),
            new WeightedNamePart("zel")
        };

        private static readonly WeightedNamePart[] CommonSuffixes =
        {
            new WeightedNamePart("kind"),
            new WeightedNamePart("man"),
            new WeightedNamePart("an"),
            new WeightedNamePart("ar"),
            new WeightedNamePart("dol"),
            new WeightedNamePart("dal"),
            new WeightedNamePart("er", 3f),
            new WeightedNamePart("en", 2f),
            new WeightedNamePart("ex"),
            new WeightedNamePart("id"),
            new WeightedNamePart("in"),
            new WeightedNamePart("il"),
            new WeightedNamePart("ist"),
            new WeightedNamePart("on"),
            new WeightedNamePart("ol"),
            new WeightedNamePart("ox", 2f),
            new WeightedNamePart("ub"),
            new WeightedNamePart("ul"),
            new WeightedNamePart("ur"),
            new WeightedNamePart("ux")
        };

        private static readonly WeightedNamePart[] EnhancedPrefixes =
        {
            new WeightedNamePart("helix", 1.2f),
            new WeightedNamePart("splice", 1.2f),
            new WeightedNamePart("strain", 0.8f),
            new WeightedNamePart("nova", 0.8f),
            new WeightedNamePart("prime", 0.7f),
            new WeightedNamePart("apex", 0.9f),
            new WeightedNamePart("vita", 0.8f),
            new WeightedNamePart("muta", 0.9f),
            new WeightedNamePart("morph", 0.9f),
            new WeightedNamePart("chrom", 0.8f),
            new WeightedNamePart("neuro", 0.8f),
            new WeightedNamePart("psy", 0.7f),
            new WeightedNamePart("cryo", 0.6f),
            new WeightedNamePart("therm", 0.6f),
            new WeightedNamePart("ferro", 0.6f),
            new WeightedNamePart("umbra", 0.7f),
            new WeightedNamePart("lumen", 0.7f),
            new WeightedNamePart("soma", 0.7f),
            new WeightedNamePart("exo", 0.7f),
            new WeightedNamePart("proto", 0.7f)
        };

        private static readonly WeightedNamePart[] EnhancedSuffixes =
        {
            new WeightedNamePart("kin", 1.4f),
            new WeightedNamePart("born", 1.3f),
            new WeightedNamePart("blood", 1.1f),
            new WeightedNamePart("line", 1.1f),
            new WeightedNamePart("strain", 1.2f),
            new WeightedNamePart("clade", 1.1f),
            new WeightedNamePart("form", 0.9f),
            new WeightedNamePart("morph", 1.0f),
            new WeightedNamePart("breed", 0.8f),
            new WeightedNamePart("cast", 0.8f),
            new WeightedNamePart("bound", 0.8f),
            new WeightedNamePart("host", 0.7f),
            new WeightedNamePart("core", 0.7f),
            new WeightedNamePart("mark", 0.7f),
            new WeightedNamePart("wake", 0.6f),
            new WeightedNamePart("veil", 0.6f),
            new WeightedNamePart("loom", 0.6f),
            new WeightedNamePart("node", 0.6f),
            new WeightedNamePart("skin", 0.6f)
        };

        private static readonly WeightedNamePart[] LargerPackTitles =
        {
            new WeightedNamePart("clade", 1.4f),
            new WeightedNamePart("lineage", 1.2f),
            new WeightedNamePart("strain", 1.2f),
            new WeightedNamePart("genome", 1.0f),
            new WeightedNamePart("complex", 0.9f),
            new WeightedNamePart("variant", 0.9f),
            new WeightedNamePart("concord", 0.6f),
            new WeightedNamePart("sequence", 0.8f),
            new WeightedNamePart("matrix", 0.6f),
            new WeightedNamePart("stock", 0.6f),
            new WeightedNamePart("kindred", 0.8f)
        };

        private struct WeightedNamePart
        {
            public readonly string Text;
            public readonly float Weight;

            public WeightedNamePart(string text, float weight = 1f)
            {
                Text = text;
                Weight = weight;
            }
        }

        // Helper to ensure the preview window is open and correctly configured
        private static void EnsurePreviewWindowOpen(Window dialogInstance)
        {
            if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
            {
                XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
            }

            XenoPreview.PreviewWindowInstance.SetDialog(dialogInstance);
            // I don't want to set it manually, but this window doesn't know its size till after this one is opened. I tried to use a PostOpen Postfix patch, but it didn't work.
            XenoPreview.PreviewWindowInstance.UpdatePosition(new Vector2(1474, 1009));
        }

        public static bool GenerateXenotypeNameFromGenes_Prefix(List<GeneDef> genes, ref string __result)
        {
            try
            {
                if (!IsExplicitNameRandomizationCall())
                {
                    if (TryGetCurrentDialogXenotypeName(out string currentName))
                    {
                        __result = currentName;
                        return false;
                    }

                    return true;
                }

                __result = GenerateUniqueXenotypeNameFromGenes(genes);
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning("[XenoPreview] Failed to generate patched xenotype name; using fallback name. " + ex);
                __result = GenerateFallbackXenotypeName();
                return false;
            }
        }

        private static bool IsExplicitNameRandomizationCall()
        {
            StackTrace stackTrace = new StackTrace();
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                MethodBase method = stackTrace.GetFrame(i).GetMethod();
                if (method == null)
                {
                    continue;
                }

                if (method.Name == "DrawNameInput" &&
                    method.DeclaringType != null &&
                    method.DeclaringType == typeof(GeneCreationDialogBase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetCurrentDialogXenotypeName(out string currentName)
        {
            currentName = null;
            try
            {
                if (Find.WindowStack == null)
                {
                    return false;
                }

                foreach (Window window in Find.WindowStack.Windows)
                {
                    if (window is GeneCreationDialogBase && window.IsOpen)
                    {
                        currentName = Traverse.Create(window).Field("xenotypeName").GetValue<string>();
                        return currentName != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[XenoPreview] Could not preserve current xenotype name during gene change. " + ex);
            }

            return false;
        }

        private static string GenerateUniqueXenotypeNameFromGenes(List<GeneDef> genes)
        {
            if (genes == null || genes.Count == 0)
            {
                return string.Empty;
            }

            List<GeneSymbolPack> symbolPacks = genes
                .Where(gene => gene != null && gene.symbolPack != null)
                .Select(gene => gene.symbolPack)
                .ToList();
            string lastCandidate = string.Empty;

            for (int i = 0; i < MaxXenotypeNameAttempts; i++)
            {
                string candidate = GenerateEnhancedXenotypeName(symbolPacks, genes.Count);
                if (candidate.NullOrEmpty())
                {
                    continue;
                }

                lastCandidate = candidate;
                if (!XenotypeNameIsUsed(candidate))
                {
                    return candidate;
                }
            }

            if (!lastCandidate.NullOrEmpty())
            {
                Log.Warning("[XenoPreview] Generated xenotype names were already used after " + MaxXenotypeNameAttempts + " attempts; returning the last random candidate anyway.");
                return lastCandidate;
            }

            Log.Warning("[XenoPreview] Failed to generate a xenotype name after " + MaxXenotypeNameAttempts + " attempts.");
            return GenerateFallbackXenotypeName();
        }

        private static string GenerateEnhancedXenotypeName(List<GeneSymbolPack> symbolPacks, int geneCount)
        {
            if (TryGenerateWholeName(symbolPacks, out string wholeName) && Rand.Value < WholeNameChance(geneCount))
            {
                return CleanGeneratedName(wholeName);
            }

            string prefix = GeneratePrefix(symbolPacks, out GeneSymbolPack prefixPack);
            string suffix = GenerateSuffix(symbolPacks, prefixPack, prefix);
            string meaningfulPrefix = GenerateMeaningfulPrefix(symbolPacks, prefixPack);
            string primaryPrefix = geneCount >= 6 ? GenerateMeaningfulPrefix(symbolPacks, null) : prefix;
            string enhancedPrefix = RandomCommonPart(EnhancedPrefixes);
            string enhancedSuffix = RandomCommonPart(EnhancedSuffixes);
            string meaningfulSuffix = GenerateMeaningfulSuffix(symbolPacks, prefixPack, primaryPrefix);
            string title = RandomCommonPart(LargerPackTitles);
            float roll = Rand.Value;

            if (geneCount >= 10)
            {
                if (roll < 0.25f)
                    return CleanGeneratedName(primaryPrefix + meaningfulSuffix + " " + title);
                if (roll < 0.45f)
                    return CleanGeneratedName(primaryPrefix + "-" + meaningfulSuffix + " " + title);
                if (roll < 0.62f)
                    return CleanGeneratedName(primaryPrefix + " " + meaningfulPrefix + " " + title);
                if (roll < 0.78f)
                    return CleanGeneratedName(enhancedPrefix + meaningfulSuffix + " " + title);
                if (roll < 0.90f)
                    return CleanGeneratedName(primaryPrefix + enhancedSuffix);
                return CleanGeneratedName(GenerateVanillaStyleXenotypeName(symbolPacks));
            }

            if (geneCount >= 6)
            {
                if (roll < 0.30f)
                    return CleanGeneratedName(primaryPrefix + suffix);
                if (roll < 0.50f)
                    return CleanGeneratedName(primaryPrefix + "-" + meaningfulSuffix);
                if (roll < 0.68f)
                    return CleanGeneratedName(primaryPrefix + " " + title);
                if (roll < 0.83f)
                    return CleanGeneratedName(enhancedPrefix + meaningfulSuffix);
                return CleanGeneratedName(primaryPrefix + enhancedSuffix);
            }

            if (roll < 0.55f)
                return CleanGeneratedName(prefix + suffix);
            if (roll < 0.75f)
                return CleanGeneratedName(prefix + enhancedSuffix);
            if (roll < 0.90f)
                return CleanGeneratedName(enhancedPrefix + meaningfulSuffix);
            return CleanGeneratedName(GenerateVanillaStyleXenotypeName(symbolPacks));
        }

        private static float WholeNameChance(int geneCount)
        {
            if (geneCount >= 10)
                return 0.05f;
            if (geneCount >= 6)
                return 0.08f;
            return 0.12f;
        }

        private static string GenerateVanillaStyleXenotypeName(List<GeneSymbolPack> symbolPacks)
        {
            if (TryGenerateWholeName(symbolPacks, out string wholeName) && Rand.Value < 0.1f)
            {
                return wholeName;
            }

            string prefix = GeneratePrefix(symbolPacks, out GeneSymbolPack prefixPack);
            string suffix = GenerateSuffix(symbolPacks, prefixPack, prefix);

            if (prefix.NullOrEmpty() && suffix.NullOrEmpty())
            {
                return GenerateFallbackXenotypeName();
            }

            return prefix + suffix;
        }

        private static string GenerateMeaningfulSuffix(List<GeneSymbolPack> symbolPacks, GeneSymbolPack prefixPack, string prefixText)
        {
            if (TryRandomSymbolPack(symbolPacks, pack => pack.suffixSymbols, out GeneSymbolPack suffixPack, prefixPack) &&
                TryRandomSymbol(suffixPack.suffixSymbols, new GeneSymbolPack.WeightedSymbol { symbol = prefixText, weight = 1f }, null, out GeneSymbolPack.WeightedSymbol suffix) &&
                !suffix.symbol.NullOrEmpty() &&
                suffix.symbol.Length >= 4)
            {
                return suffix.symbol;
            }

            return RandomCommonPart(EnhancedSuffixes);
        }

        private static string GenerateMeaningfulPrefix(List<GeneSymbolPack> symbolPacks, GeneSymbolPack excludedPack)
        {
            if (TryRandomSymbolPack(symbolPacks, pack => pack.prefixSymbols, out GeneSymbolPack prefixPack, excludedPack) &&
                TryRandomSymbol(prefixPack.prefixSymbols, null, null, out GeneSymbolPack.WeightedSymbol prefix) &&
                !prefix.symbol.NullOrEmpty() &&
                prefix.symbol.Length >= 3)
            {
                return prefix.symbol;
            }

            return RandomCommonPart(EnhancedPrefixes);
        }

        private static string CleanGeneratedName(string name)
        {
            if (name.NullOrEmpty())
            {
                return string.Empty;
            }

            name = name.Trim();
            while (name.Contains("--"))
                name = name.Replace("--", "-");
            while (name.Contains("  "))
                name = name.Replace("  ", " ");
            name = name.Trim(' ', '-');

            if (name.Length > 36)
            {
                name = name.Substring(0, 36).Trim(' ', '-');
            }

            return CapitalizeWords(name);
        }

        private static string CapitalizeWords(string name)
        {
            char[] chars = name.ToCharArray();
            bool capitalizeNext = true;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetter(c))
                {
                    chars[i] = capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = c == ' ' || c == '-';
                }
            }

            return new string(chars);
        }

        private static bool TryGenerateWholeName(List<GeneSymbolPack> symbolPacks, out string wholeName)
        {
            wholeName = null;
            if (!TryRandomSymbolPack(symbolPacks, pack => pack.wholeNameSymbols, out GeneSymbolPack wholeNamePack))
            {
                return false;
            }

            if (!TryRandomSymbol(wholeNamePack.wholeNameSymbols, null, null, out GeneSymbolPack.WeightedSymbol wholeNameSymbol))
            {
                return false;
            }

            wholeName = wholeNameSymbol.symbol;
            return !wholeName.NullOrEmpty();
        }

        private static string GeneratePrefix(List<GeneSymbolPack> symbolPacks, out GeneSymbolPack prefixPack)
        {
            prefixPack = null;
            if (Rand.Range(0f, 3f) < 2f &&
                TryRandomSymbolPack(symbolPacks, pack => pack.prefixSymbols, out prefixPack) &&
                TryRandomSymbol(prefixPack.prefixSymbols, null, null, out GeneSymbolPack.WeightedSymbol prefix))
            {
                return prefix.symbol;
            }

            prefixPack = null;
            return RandomCommonPart(CommonPrefixes);
        }

        private static string GenerateSuffix(List<GeneSymbolPack> symbolPacks, GeneSymbolPack prefixPack, string prefixText)
        {
            if (Rand.Range(0f, 4f) < 1f &&
                TryRandomSymbolPack(symbolPacks, pack => pack.suffixSymbols, out GeneSymbolPack suffixPack, prefixPack) &&
                TryRandomSymbol(suffixPack.suffixSymbols, new GeneSymbolPack.WeightedSymbol { symbol = prefixText, weight = 1f }, null, out GeneSymbolPack.WeightedSymbol suffix))
            {
                return suffix.symbol;
            }

            return RandomCommonPart(CommonSuffixes);
        }

        private static string RandomCommonPart(WeightedNamePart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            float totalWeight = parts.Where(part => !part.Text.NullOrEmpty() && part.Weight > 0f).Sum(part => part.Weight);
            if (totalWeight <= 0f)
            {
                return string.Empty;
            }

            float value = Rand.Value * totalWeight;
            for (int i = 0; i < parts.Length; i++)
            {
                WeightedNamePart part = parts[i];
                if (part.Text.NullOrEmpty() || part.Weight <= 0f)
                {
                    continue;
                }

                value -= part.Weight;
                if (value <= 0f)
                {
                    return part.Text;
                }
            }

            return parts[parts.Length - 1].Text;
        }

        private static bool TryRandomSymbolPack(
            List<GeneSymbolPack> symbolPacks,
            Func<GeneSymbolPack, List<GeneSymbolPack.WeightedSymbol>> symbolsForPack,
            out GeneSymbolPack result,
            GeneSymbolPack excludedPack = null)
        {
            return symbolPacks
                .Where(pack => pack != excludedPack && HasSymbols(symbolsForPack(pack)))
                .TryRandomElementByWeight(pack => SymbolPackWeight(symbolsForPack(pack)), out result);
        }

        private static bool TryRandomSymbol(
            List<GeneSymbolPack.WeightedSymbol> symbols,
            GeneSymbolPack.WeightedSymbol prefix,
            GeneSymbolPack.WeightedSymbol suffix,
            out GeneSymbolPack.WeightedSymbol result)
        {
            if (!HasSymbols(symbols))
            {
                result = null;
                return false;
            }

            return symbols.TryRandomElementByWeight(symbol => SymbolWeight(symbol, prefix, suffix), out result);
        }

        private static bool HasSymbols(List<GeneSymbolPack.WeightedSymbol> symbols)
        {
            return symbols != null && symbols.Any(symbol => SymbolWeight(symbol, null, null) > 0f);
        }

        private static float SymbolPackWeight(List<GeneSymbolPack.WeightedSymbol> symbols)
        {
            if (symbols == null)
            {
                return 0f;
            }

            return symbols.Sum(symbol => SymbolWeight(symbol, null, null));
        }

        private static float SymbolWeight(
            GeneSymbolPack.WeightedSymbol symbol,
            GeneSymbolPack.WeightedSymbol prefix,
            GeneSymbolPack.WeightedSymbol suffix)
        {
            if (symbol == null || symbol.symbol.NullOrEmpty() || symbol.weight <= 0f)
            {
                return 0f;
            }

            if ((prefix != null && symbol.symbol == prefix.symbol) ||
                (suffix != null && symbol.symbol == suffix.symbol))
            {
                return 0f;
            }

            return symbol.weight;
        }

        private static string GenerateFallbackXenotypeName()
        {
            try
            {
                const string baseName = "xenotype";
                for (int i = 1; i <= 10000; i++)
                {
                    string candidate = i == 1 ? baseName : baseName + " " + i;
                    if (!XenotypeNameIsUsed(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[XenoPreview] Failed to generate fallback xenotype name. " + ex);
            }

            return "ERR";
        }

        private static bool XenotypeNameIsUsed(string candidate)
        {
            try
            {
                return NameUseChecker.XenotypeNameIsUsed(candidate);
            }
            catch (Exception ex)
            {
                Log.Warning("[XenoPreview] Could not check whether xenotype name is already used; allowing random candidate. " + ex);
                return false;
            }
        }

        public static void PreOpen_Prefix(GeneCreationDialogBase __instance)
        {
            try
            {
                EnsurePreviewWindowOpen(__instance);
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Error in PostOpen_Postfix: " + ex.ToString());
            }
        }

        // Postfix for Close
        public static void Close_Postfix(Window __instance)
        {
            try
            {
                // Check if it's Dialog_CreateXenotype or Dialog_CreateXenogerm
                if (
                    !(__instance is Dialog_CreateXenotype)
                    && !(__instance is Dialog_CreateXenogerm)
                )
                {
                    return;
                }

                if (
                    XenoPreview.PreviewWindowInstance != null
                    && XenoPreview.PreviewWindowInstance.IsOpen
                )
                {
                    XenoPreview.PreviewWindowInstance.Close(false);
                    XenoPreview.PreviewWindowInstance = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Error in Close_Postfix: " + ex.ToString());
            }
        }
    }
}
