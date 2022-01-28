﻿// Copyright (c) 2019 v1ld.git@gmail.com
// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.ServiceWindow.LocalMap;
using Kingmaker.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityModManagerNet;

namespace CustomMapMarkers
{
    public class Main
    {
        [Harmony12.HarmonyPatch(typeof(LibraryScriptableObject), "LoadDictionary", new Type[0])]
        static class LibraryScriptableObject_LoadDictionary_Patch
        {
            static void Postfix(LibraryScriptableObject __instance)
            {
                var self = __instance;
                if (Main.library != null) return;
                Main.library = self;

                //EnableGameLogging();

#if DEBUG
                // Perform extra sanity checks in debug builds.
                SafeLoad(CheckPatchingSuccess, "Check that all patches are used, and were loaded");
                Log.Write("Load finished.");
#endif
            }
        }

        [Harmony12.HarmonyPatch(typeof(LocalMap), "OnPointerClick")]
        static class LocalMap_OnPointerClick_Patch
        {
            private static bool Prefix(LocalMap __instance, PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    if (IsShiftPressed)
                    {
                        CustomMapMarkers.CreateMarker(__instance, eventData);
                    }
                }

                // Don't pass the click through to the map if control or shift are pressed
                return !(IsControlPressed || IsShiftPressed);
            }
        }

        [Harmony12.HarmonyPatch(typeof(LocalMap), "OnShow")]
        static class LocalMap_OnShow_Patch
        {
            private static bool Prefix(LocalMap __instance)
            {
                LocalMapRef = __instance;
                
                CustomMapMarkers.OnShowLocalMap();
                return true;
            }
        }

        [Harmony12.HarmonyPatch(typeof(LocalMap), "OnHide")]
        static class LocalMap_OnHide_Patch
        {
            private static void Postfix()
            {
                LocalMapRef = null;
            }
        }

        [Harmony12.HarmonyPatch(typeof(GlobalMapLocation), "HandleClick")]
        static class GlobalMapLocation_HandleClick_Patch
        {
            private static bool Prefix(GlobalMapLocation __instance)
            {
                if (IsShiftPressed)
                {
                    CustomGlobalMapLocations.CustomizeGlobalMapLocation(__instance);
                }
                // Don't pass the click through to the map if control or shift are pressed
                return !(IsControlPressed || IsShiftPressed);
            }
        }
        /*
                [Harmony12.HarmonyPatch(typeof(GlobalMapLocation), "HandleHoverChange")]
                static class GlobalMapLocation_HandleHoverChange_Patch
                {
                    private static void Postfix(GlobalMapLocation __instance, bool isHover)
                    {
                        CustomGlobalMapLocations.PostHandleHoverchange(__instance, isHover);
                    }
                }
        */
        [Harmony12.HarmonyPatch(typeof(GlobalMapLocation), "UpdateHighlight")]
        static class GlobalMapLocation_UpdateHighlight_Patch
        {
            private static void Postfix(GlobalMapLocation __instance)
            {
                CustomGlobalMapLocations.PostUpdateHighlight(__instance);
            }
        }
        [Harmony12.HarmonyPatch(typeof(BlueprintLocation), "GetDescription")]
        static class BlueprintLocation_GetDescription_Patch
        {
            private static void Postfix(BlueprintLocation __instance, ref string __result)
            {
                __result = ModGlobalMapLocation.GetModifiedDescription(__instance, __result);
            }
        }

        [Harmony12.HarmonyPatch(typeof(UnityModManager.UI), "Update")]
        static class UnityModManager_UI_Update_Patch
        {
            private static void Postfix()
            {
                if (IsLocalMapActive || Game.Instance.CurrentMode == GameModeType.GlobalMap)
                {
                    try
                    {
                        IsControlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                        IsShiftPressed   = Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift);
                    }
                    catch (Exception e)
                    {
                        Log.Write($"Key read: {e}");
                    }
                }
            }
        }

        private static bool IsLocalMapActive => LocalMapRef != null;
        internal static LocalMap LocalMapRef;
        private static bool IsControlPressed = false;
        private static bool IsShiftPressed   = false;

        static LibraryScriptableObject library;

        public static bool enabled;

        public static UnityModManager.ModEntry.ModLogger logger;

        static Settings settings;

        static Harmony12.HarmonyInstance harmonyInstance;

        static readonly Dictionary<Type, bool> typesPatched = new Dictionary<Type, bool>();
        static readonly List<String> failedPatches = new List<String>();
        static readonly List<String> failedLoading = new List<String>();

        [System.Diagnostics.Conditional("DEBUG")]
        static void EnableGameLogging()
        {
            if (UberLogger.Logger.Enabled) return;

            // Code taken from GameStarter.Awake(). PF:K logging can be enabled with command line flags,
            // but when developing the mod it's easier to force it on.
            var dataPath = ApplicationPaths.persistentDataPath;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            UberLogger.Logger.Enabled = true;
            var text = Path.Combine(dataPath, "GameLog.txt");
            if (File.Exists(text))
            {
                File.Copy(text, Path.Combine(dataPath, "GameLogPrev.txt"), overwrite: true);
                File.Delete(text);
            }
            UberLogger.Logger.AddLogger(new UberLoggerFile("GameLogFull.txt", dataPath));
            UberLogger.Logger.AddLogger(new UberLoggerFilter(new UberLoggerFile("GameLog.txt", dataPath), UberLogger.LogSeverity.Warning, "MatchLight"));

            UberLogger.Logger.Enabled = true;
        }

        internal static void NotifyPlayer(string message, bool warning = false)
        {
            if (warning)
            {
                EventBus.RaiseEvent<IWarningNotificationUIHandler>((IWarningNotificationUIHandler h) => h.HandleWarning(message, true));
            }
            else
            {
                Game.Instance.UI.BattleLogManager.LogView.AddLogEntry(message, GameLogStrings.Instance.DefaultColor);
            }
        }

        // We don't want one patch failure to take down the entire mod, so they're applied individually.
        //
        // Also, in general the return value should be ignored. If a patch fails, we still want to create
        // blueprints, otherwise the save won't load. Better to have something be non-functional.
        static bool ApplyPatch(Type type, String featureName)
        {
            try
            {
                if (typesPatched.ContainsKey(type)) return typesPatched[type];

                var patchInfo = Harmony12.HarmonyMethodExtensions.GetHarmonyMethods(type);
                if (patchInfo == null || patchInfo.Count() == 0)
                {
                    Log.Error($"Failed to apply patch {type}: could not find Harmony attributes");
                    failedPatches.Add(featureName);
                    typesPatched.Add(type, false);
                    return false;
                }
                var processor = new Harmony12.PatchProcessor(harmonyInstance, type, Harmony12.HarmonyMethod.Merge(patchInfo));
                var patch = processor.Patch().FirstOrDefault();
                if (patch == null)
                {
                    Log.Error($"Failed to apply patch {type}: no dynamic method generated");
                    failedPatches.Add(featureName);
                    typesPatched.Add(type, false);
                    return false;
                }
                typesPatched.Add(type, true);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply patch {type}: {e}");
                failedPatches.Add(featureName);
                typesPatched.Add(type, false);
                return false;
            }
        }

        static void CheckPatchingSuccess()
        {
            // Check to make sure we didn't forget to patch something.
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var infos = Harmony12.HarmonyMethodExtensions.GetHarmonyMethods(type);
                if (infos != null && infos.Count() > 0 && !typesPatched.ContainsKey(type))
                {
                    Log.Write($"Did not apply patch for {type}");
                }
            }
        }

        // Mod entry point, invoked from UMM
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            logger = modEntry.Logger;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            harmonyInstance = Harmony12.HarmonyInstance.Create(modEntry.Info.Id);
            if (!ApplyPatch(typeof(LibraryScriptableObject_LoadDictionary_Patch), "Load library"))
            {
                throw Error("Failed to patch LibraryScriptableObject.LoadDictionary(), cannot load mod");
            }
            if (!ApplyPatch(typeof(UnityModManager_UI_Update_Patch), "Read keys"))
            {
                throw Error("Failed to patch LibraryScriptableObject.LoadDictionary(), cannot load mod");
            }
            if (!ApplyPatch(typeof(LocalMap_OnPointerClick_Patch), "Local map click"))
            {
                throw Error("Failed to patch LocalMap.OnPointerClick(), cannot load mod");
            }
            if (!ApplyPatch(typeof(LocalMap_OnShow_Patch), "Local map show"))
            {
                throw Error("Failed to patch LocalMap.OnShow(), cannot load mod");
            }
            if (!ApplyPatch(typeof(LocalMap_OnHide_Patch), "Local map hide"))
            {
                throw Error("Failed to patch LocalMap.OnHide(), cannot load mod");
            }
            if (!ApplyPatch(typeof(GlobalMapLocation_HandleClick_Patch), "Global map location click"))
            {
                throw Error("Failed to patch GlobalMapLocation.HandleClick(), cannot load mod");
            }
            if (!ApplyPatch(typeof(GlobalMapLocation_UpdateHighlight_Patch), "Global map highlight change"))
            {
                throw Error("Failed to patch GlobalMapLocation.UpdateHighlight(), cannot load mod");
            }
            if (!ApplyPatch(typeof(BlueprintLocation_GetDescription_Patch), "Blueprint location description"))
            {
                throw Error("Failed to patch BlueprintLocation.GetDescription(), cannot load mod");
            }

            StartMod();
            return true;
        }

        static void StartMod()
        {
            SafeLoad(StateManager.Load, "State Manager");
            SafeLoad(CustomMapMarkers.Load, "Local Map Markers");
            SafeLoad(CustomGlobalMapLocations.Load, "Global Map Locations");
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        private static bool editingGlobalMapLocations = true;

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (!enabled) return;

            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };
            if (failedPatches.Count > 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("<b><color=#ff5454>Error: Some patches failed to apply. These features may not work:</color></b>", fixedWidth);
                foreach (var featureName in failedPatches)
                {
                    GUILayout.Label($"  • <b><color=#ff5454>{featureName}</b>", fixedWidth);
                }
                GUILayout.EndVertical();
            }
            if (failedLoading.Count > 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("<b><color=#ff5454>Error: Some assets failed to load. Saves using these features won't work:</color></b>", fixedWidth);
                foreach (var featureName in failedLoading)
                {
                    GUILayout.Label($"  • <b><color=#ff5454>{featureName}</color></b>", fixedWidth);
                }
                GUILayout.EndVertical();
            }
#if DEBUG
            GUILayout.BeginVertical();
            GUILayout.Label("<b>DEBUG build!</b>", fixedWidth);
            GUILayout.Space(10f);
            GUILayout.EndVertical();
#endif

            string gameCharacterName = Game.Instance.Player.MainCharacter.Value?.CharacterName;
            if (gameCharacterName == null || gameCharacterName.Length == 0)
            {
                GUILayout.Label("<b><color=#ff5454>Load a save to edit map notes and markers.</color></b>", fixedWidth);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><color=cyan>Edit: </color></b>", fixedWidth);
            bool doEditLocalMap = GUILayout.Toggle(!editingGlobalMapLocations, "<b><color=cyan>Local Map Areas</color></b>", fixedWidth);
            editingGlobalMapLocations = GUILayout.Toggle(!doEditLocalMap, "<b><color=cyan>Global Map Locations</color></b>", fixedWidth);
            GUILayout.EndHorizontal();

            if (editingGlobalMapLocations)
            {
                CustomGlobalMapLocationsMenu.Layout();
            }
            else
            {
                CustomMapMarkersMenu.Layout();
            }
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
            StateManager.SaveState();
        }

        static void SafeLoad(Action load, String name)
        {
            try
            {
                load();
            }
            catch (Exception e)
            {
                failedLoading.Add(name);
                Log.Error(e);
            }
        }

        static T SafeLoad<T>(Func<T> load, String name)
        {
            try
            {
                return load();
            }
            catch (Exception e)
            {
                failedLoading.Add(name);
                Log.Error(e);
                return default(T);
            }
        }

        static Exception Error(String message)
        {
            logger?.Log(message);
            return new InvalidOperationException(message);
        }
    }


    public class Settings : UnityModManager.ModSettings
    {
        public bool SaveAfterEveryChange = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            UnityModManager.ModSettings.Save<Settings>(this, modEntry);
        }
    }
}
