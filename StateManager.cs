// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.Utility;

namespace CustomMapMarkers
{
    class StateManager : IWarningNotificationUIHandler
    {
        [DataContract]
        public class SavedState
        {
            [DataMember(Order=1)]
            public readonly uint Version = 1;   // Data version for serialization
            [DataMember(Order=2)]
            public uint MarkerNumber = 1;       // Used in creating marker names
            [DataMember(Order=100)]
            public HashSet<ModGlobalMapLocation> GlobalMapLocations { get; private set; }
            [DataMember(Order=101)]
            public Dictionary<string, List<ModMapMarker>> AreaMarkers { get; private set; }

            public SavedState()
            {
                GlobalMapLocations = new HashSet<ModGlobalMapLocation>();
                AreaMarkers = new Dictionary<string, List<ModMapMarker>>();
            }

            public void ValidateAfterLoad()
            {
                if (GlobalMapLocations == null) { GlobalMapLocations = new HashSet<ModGlobalMapLocation>(); }
                if (AreaMarkers == null) { AreaMarkers  = new Dictionary<string, List<ModMapMarker>>(); }
            }

            public SavedState CleanCopyForSave()
            {
                var clone = (SavedState)this.MemberwiseClone();
                clone.GlobalMapLocations = StateHelpers.PurgeDeletedGlobalMapLocations(this.GlobalMapLocations);
                clone.AreaMarkers = StateHelpers.PurgeDeletedAreaMarkers(this.AreaMarkers);
                return clone;
            }
        }

        public static SavedState CurrentState;

        internal static void Load()
        {
            EventBus.Subscribe(new StateManager());
        }

        public static void LoadState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, GetStateFileName());
            if (File.Exists(stateFile))
            {
                try
                {
                    using (FileStream reader = new FileStream(stateFile, FileMode.Open))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SavedState));
                        CurrentState = (SavedState)serializer.ReadObject(reader);
                        CurrentState.ValidateAfterLoad();
                        reader.Close();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to load state: {e}");
                    // Move the unreadable state file somewhere safe for now
                    if (File.Exists(stateFile))
                    {
                        string tempFileName = GetStateFileName("-unreadable-" + Path.GetRandomFileName());
                        string newStateFile = Path.Combine(ApplicationPaths.persistentDataPath, tempFileName);
                        File.Move(stateFile, newStateFile);
                        Log.Write($"Moved unreadable state file to {tempFileName}");
                    }
                }
            }

            // Must have a valid state at all times.
            // This catches both first use and load errors.
            CurrentState = new SavedState();
        }

        public static void SaveState()
        {
            string tempFileName = GetStateFileName("-new-" + Path.GetRandomFileName());
            string newStateFile = Path.Combine(ApplicationPaths.persistentDataPath, tempFileName);
            try
            {
                using (FileStream writer = new FileStream(newStateFile, FileMode.Create))
                {
                    var savedState = CurrentState.CleanCopyForSave();
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SavedState));
                    serializer.WriteObject(writer, savedState);
                    writer.Close();     // must explicitly Close() before File.Move()

                    string originalStateFile = Path.Combine(ApplicationPaths.persistentDataPath, GetStateFileName());
                    File.Delete(originalStateFile);
                    File.Move(newStateFile, originalStateFile);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to SaveState: {e}");
                if (File.Exists(newStateFile))
                {
                    File.Delete(newStateFile);
                }
            }
        }

        private static string StateFilenameBase = "custom-map-markers-state";
        private static string StateFilenameExt = ".json";

        private static string GetStateFileName(string suffix = "")
        {
            return StateFilenameBase + suffix + StateFilenameExt;
        }

        void IWarningNotificationUIHandler.HandleWarning(WarningNotificationType warningType, bool addToLog)
        {
            switch (warningType)
            {
                case WarningNotificationType.GameSaved:
                case WarningNotificationType.GameSavedAuto:
                case WarningNotificationType.GameSavedQuick:
                    SaveState();
                    break;
            }
        }

        void IWarningNotificationUIHandler.HandleWarning(string text, bool addToLog) { }
    }

    class StateHelpers
    {
        public static Dictionary<string, List<ModMapMarker>> PurgeDeletedAreaMarkers(Dictionary<string, List<ModMapMarker>> oldMarkers)
        {
            Dictionary<string, List<ModMapMarker>> newMarkers = new Dictionary<string, List<ModMapMarker>>();
            foreach (var area in oldMarkers.Keys)
            {
                if (oldMarkers[area].Any(marker => marker.IsDeleted))
                {
                    List<ModMapMarker> markers = oldMarkers[area].FindAll(marker => !marker.IsDeleted);
                    if (markers.Count() > 0)
                    {
                        newMarkers[area] = markers;
                    }
                }
                else
                {
                    newMarkers[area] = oldMarkers[area];
                }
            }
            return newMarkers;
        }

        internal static HashSet<ModGlobalMapLocation> PurgeDeletedGlobalMapLocations(HashSet<ModGlobalMapLocation> oldLocations)
        {
            HashSet<ModGlobalMapLocation> newLocations;
            if (oldLocations.Any(location => location.IsDeleted))
            {

                newLocations = new HashSet<ModGlobalMapLocation>(oldLocations.Where(location => !location.IsDeleted));
            }
            else
            {
                newLocations = oldLocations;
            }

            return newLocations;
        }
    }
}
