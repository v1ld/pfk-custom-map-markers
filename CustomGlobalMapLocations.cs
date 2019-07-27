// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.PubSubSystem;
using UnityEngine;

namespace CustomMapMarkers
{
    class CustomGlobalMapLocations : IGameModeHandler
    {
        internal static void Load()
        {
            EventBus.Subscribe(new CustomGlobalMapLocations());
        }

        internal static void CustomizeGlobalMapLocation(GlobalMapLocation location)
        {
            ModGlobalMapLocation mapLocation = ModGlobalMapLocation.FindOrCreateByAssetGuid(location.Blueprint.AssetGuid);
            if (mapLocation != null)
            {
                mapLocation.UpdateGlobalMapLocation();
            }
            else
            {
                Log.Error($"Could not findOrCreate location name=[{location.Blueprint.GetName(false)}] guid=[{location.Blueprint.AssetGuid}]");
            }
        }

        void IGameModeHandler.OnGameModeStart(GameModeType gameMode)
        {
            if (gameMode == GameModeType.GlobalMap)
            {
                ModGlobalMapLocation.AddGlobalMapLocations();
            }
        }

        void IGameModeHandler.OnGameModeStop(GameModeType gameMode)
        {
            if (gameMode == GameModeType.GlobalMap)
            {
                StateManager.SaveState();
            }
        }

        internal static void PostHandleHoverchange(GlobalMapLocation location, bool isHover)
        {
            if (!isHover)
            {
                var mapLocation = ModGlobalMapLocation.FindByAssetGuid(location.Blueprint.AssetGuid);
                if (mapLocation != null && !mapLocation.IsDeleted && mapLocation.IsVisible)
                {
                    mapLocation.UpdateGlobalMapLocation();
                }
            }
        }
    }

    [DataContract]
    class ModGlobalMapLocation
	{
        private static HashSet<ModGlobalMapLocation> GlobalMapLocations { get { return StateManager.CurrentState.GlobalMapLocations; } }
        public static bool IsInitialized { get; private set; } = false;

        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public Color CurrentColor { get; set; }
        [DataMember]
        public bool IsVisible { get; set; }
        [DataMember]
        private string AssetGuid;

        private GlobalMapLocation mapLocation;
        private string originalDescription;
        private Color originalColor;
        public bool IsDeleted = false;
        public bool IsBeingDeleted = false;

        public string Name { get { return mapLocation.Blueprint.GetName(false); } }

        private ModGlobalMapLocation(GlobalMapLocation location)
        {
            this.mapLocation         = location;
            this.AssetGuid           = location.Blueprint.AssetGuid;
            this.originalDescription = location.Blueprint.Description;
            this.originalColor       = location.HoverColor;

            this.Description = $"Custom Global Map Location #{StateManager.CurrentState.MarkerNumber++}";
            this.CurrentColor = Color.green;
            this.IsVisible = true;

            GlobalMapLocations.Add(this);
        }

        public static ModGlobalMapLocation FindOrCreateByAssetGuid(string assetGuid)
        {
            var modLocation = GlobalMapLocations.FirstOrDefault(location => location.AssetGuid == assetGuid);
            if (modLocation == null)
            {
                GlobalMapLocation mapLocation = GlobalMapLocation.Instances.FirstOrDefault(map => map.Blueprint.AssetGuid == assetGuid);
                if (mapLocation != null)
                {
                    modLocation = new ModGlobalMapLocation(mapLocation);
                }
                else
                {
                    Log.Write($"Cannot find GlobalMapLocation for assetGuid=[{assetGuid}]");
                }
            }
            return modLocation;
        }

        public static ModGlobalMapLocation FindByAssetGuid(string assetGuid)
            => GlobalMapLocations.FirstOrDefault(location => location.AssetGuid == assetGuid);

        internal static string GetModifiedDescription(BlueprintLocation bpLocation, string result)
        {
            ModGlobalMapLocation mapLocation = GlobalMapLocations.FirstOrDefault(location => location.AssetGuid == bpLocation.AssetGuid);
            if (mapLocation != null)
            {
                return result + "\n\n" + $"<b>Notes\n</b> <i>{mapLocation.Description}</i>";
            }
            else
            {
                return result;
            }
        }

        public bool UpdateGlobalMapLocation()
        {
            if (this.IsDeleted)
            {
                return true;
            }

            if (this.mapLocation == null)
            {
                this.mapLocation = GlobalMapLocation.Instances.FirstOrDefault(map => map.Blueprint.AssetGuid == this.AssetGuid);
                if (this.mapLocation == null)
                {
                    Log.Error($"Cannot find GlobalMapLocation for assetGuid=[{this.AssetGuid}]");
                    return false;
                }
                this.originalDescription = mapLocation.Blueprint.Description;
                this.originalColor = mapLocation.CurrentColor;
            }

            if (this.IsVisible)
            {
                this.mapLocation.HoverColor = this.CurrentColor;
                this.mapLocation.OverrideHCol = true;
            }

            // Don't have a direct way to set a highlight color on the map icon,
            // so fake it by marking customized locations as being hovered.
            Helpers.SetField(this.mapLocation, "m_Hover", this.IsVisible);

            this.mapLocation.UpdateHighlight();
            return true;
        }

        internal static void AddGlobalMapLocations()
        {
            foreach (var location in GlobalMapLocations)
            {
                if (!location.UpdateGlobalMapLocation())
                {
                    Log.Error($"Malformed location=[{location.AssetGuid}]");
                }
            }
            IsInitialized = true;
        }
    }

    class CustomGlobalMapLocationsMenu
    {
        private static HashSet<ModGlobalMapLocation> GlobalMapLocations { get { return StateManager.CurrentState.GlobalMapLocations; } }
        private static string[] ColorNames = { "Black", "Blue", "Cyan", "Gray", "Green", "Magenta", "Red", "White", "Yellow" };
        private static Color[] Colors = { Color.black, Color.blue, Color.cyan, Color.gray, Color.green, Color.magenta, Color.red, Color.white, Color.yellow };

        internal static void Layout()
        {
            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };

            if (!ModGlobalMapLocation.IsInitialized)
            {
                GUILayout.Label("<b><color=red>Location names are unavailable until the global map is first used.</color></b>", fixedWidth);
            }
            GUILayout.Label("<b><color=cyan>Descriptions can have multiple lines and paragraphs.</color></b>", fixedWidth);

            uint locationNumber = 1;
            foreach (var location in GlobalMapLocations)
            {
                if (location.IsDeleted) { continue; }

                GUILayout.Space(10f);

                string locationLabel = $"{locationNumber++}: {(ModGlobalMapLocation.IsInitialized ? location.Name : location.Description)}";
                if (location.IsVisible) { locationLabel = $"<color=#1aff1a><b>{locationLabel}</b></color>"; }
                GUILayout.Label(locationLabel, fixedWidth);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Description: ", fixedWidth);
                location.Description = GUILayout.TextArea(location.Description, GUILayout.MaxWidth(250f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Type: ", fixedWidth);
                for (int i = 0; i < ColorNames.Length; i++)
                {
                    if (GUILayout.Toggle(location.CurrentColor == Colors[i], ColorNames[i], fixedWidth))
                    {
                        location.CurrentColor = Colors[i];
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(location.IsVisible ? "Hide" : "Show", fixedWidth))
                {
                    location.IsVisible = !location.IsVisible;
                }
                if (!location.IsBeingDeleted && GUILayout.Button("Delete", fixedWidth))
                {
                    location.IsBeingDeleted = true;
                }
                if (location.IsBeingDeleted)
                {
                    GUILayout.Label("Are you sure?", fixedWidth);
                    if (GUILayout.Button("Yes", fixedWidth))
                    {
                        location.IsDeleted = true;
                        location.IsVisible = false;
                    }
                    if (GUILayout.Button("No", fixedWidth))
                    {
                        location.IsBeingDeleted = false;
                    }
                }
                GUILayout.EndHorizontal();

            }
        }
    }
}