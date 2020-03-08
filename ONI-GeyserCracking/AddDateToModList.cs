using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HexiGeyserCracking
{
    [Harmony.HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
    class AddDateToModList
    {
        private static string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        private static string FormatDay(int day) {
            switch (day % 10) {
                case 1: return $"{day}st";
                case 2: return $"{day}nd";
                case 3: return $"{day}rd";
                default:  return $"{day}th";
            }
        }

        public static void Postfix(Transform ___entryParent) {
            string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            for (int i = 0; i < Global.Instance.modManager.mods.Count; ++i) {
                KMod.Mod mod = Global.Instance.modManager.mods[i];
                if ((mod.loaded_content & KMod.Content.DLL) == KMod.Content.DLL) {
                    if (string.Equals(Path.GetFullPath(mod.label.install_path), currentPath)) {
                        string modTitle = mod.label.title;
                        for (int j = 0; j < ___entryParent.childCount; ++j) {
                            Transform modSpecificTransform = ___entryParent.GetChild(j);
                            HierarchyReferences hierarchyReferences = modSpecificTransform.GetComponent<HierarchyReferences>();
                            LocText titleReference = hierarchyReferences.GetReference<LocText>("Title");

                            if (titleReference != null && titleReference.text == modTitle) {
                                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                                titleReference.text = $"<align=left>{titleReference.text}\n" +
                                    $"<size=65%>Last updated {FormatDay(version.Build)} {months[version.Minor-1]} {version.Major} - Revision {version.Revision}";
                                titleReference.autoSizeTextContainer = false;

                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }
    }
}
