// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
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
                ModGlobalMapLocation.FindByAssetGuid(location.Blueprint.AssetGuid)?.UpdateGlobalMapLocation();
            }
        }
    }

    [DataContract]
    class ModGlobalMapLocation
	{
        private static HashSet<ModGlobalMapLocation> GlobalMapLocations { get { return StateManager.CurrentState.GlobalMapLocations; } }

        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public Color CurrentColor { get; set; }
        [DataMember]
        public bool IsActive { get; set; }
        [DataMember]
        private string AssetGuid;

        private GlobalMapLocation mapLocation;
        private string originalDescription;
        private Color originalColor;

        public bool IsDeleted = false;
        public bool IsBeingDeleted = false;

        private ModGlobalMapLocation(GlobalMapLocation location)
        {
            this.mapLocation         = location;
            this.AssetGuid           = location.Blueprint.AssetGuid;
            this.originalDescription = location.Blueprint.Description;
            this.originalColor       = location.HoverColor;

            this.Description = $"Custom Global Map Location #{StateManager.CurrentState.MarkerNumber++}";
            this.CurrentColor = Color.green;
            this.IsActive = true;

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
                return result + "\n\n" + $"<b>Notes:</b> {mapLocation.Description}";
            }
            else
            {
                return result;
            }
        }

        public bool UpdateGlobalMapLocation()
        {
            if (this.mapLocation == null)
            {
                this.mapLocation = GlobalMapLocation.Instances.FirstOrDefault(map => map.Blueprint.AssetGuid == this.AssetGuid);
                if (this.mapLocation == null)
                {
                    Log.Error($"Cannot find GlobalMapLocation for assetGuid=[{this.AssetGuid}]");
                    return false;
                }
                this.originalDescription = mapLocation.Blueprint.Description;
                this.originalColor       = mapLocation.CurrentColor;
            }

            GlobalMapLocation location = this.mapLocation;
            location.HoverColor   = this.CurrentColor;
            location.OverrideHCol = true;

            // Don't have a direct way to set a highlight color on the map icon,
            // so fake it by marking customized locations as being hovered.
            Helpers.SetField(location, "m_Hover", true);

            location.UpdateHighlight();
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
        }
    }
}
