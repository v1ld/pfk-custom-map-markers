// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Kingmaker.Utility;
using ProtoBuf;

namespace CustomMapMarkers
{
    class StateManager
    {
        [ProtoContract]
        public class SavedState
        {
            [ProtoMember(1)]
            public readonly uint Version = 1;   // Data version for serialization
            [ProtoMember(2)]
            public uint MarkerNumber = 1;       // Used in creating marker names
            [ProtoMember(128)]
            public Dictionary<string, List<ModMapMarker>> AreaMarkers { get; private set; }

            public SavedState()
            {
                AreaMarkers = new Dictionary<string, List<ModMapMarker>>();
            }

            public SavedState(Dictionary<string, List<ModMapMarker>> markers)
            {
                AreaMarkers = markers;
            }

            public SavedState CleanCopyForSave()
            {
                var clone = (SavedState)this.MemberwiseClone();
                clone.AreaMarkers = StateHelpers.PurgeDeletedMarkers(this.AreaMarkers);
                return clone;
            }
        }

        public static SavedState CurrentState;
        private static string SavedStateFile = "custom-map-markers-state.bin";

        public static void LoadState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
            if (File.Exists(stateFile))
            {
                try
                {
                    using (FileStream fs = new FileStream(stateFile, FileMode.Open))
                    {
                        CurrentState = Serializer.Deserialize<SavedState>(fs);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to load state: {e}");
                    // Move the unreadable state file somewhere safe for now
                    if (File.Exists(stateFile))
                    {
                        string tempFileName = $"{SavedStateFile}-unreadable-{Path.GetRandomFileName()}";
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
            string tempFileName = $"{SavedStateFile}-{Path.GetRandomFileName()}";
            string newStateFile = Path.Combine(ApplicationPaths.persistentDataPath, tempFileName);
            try
            {
                using (FileStream writer = new FileStream(newStateFile, FileMode.Create))
                {
                    var savedState = CurrentState.CleanCopyForSave();
                    Serializer.Serialize<SavedState>(writer, savedState);
                    writer.Close();     // Must Close() before the File.Move() below

                    string originalStateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
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
    }

    class StateHelpers
    {
        public static Dictionary<string, List<ModMapMarker>> PurgeDeletedMarkers(Dictionary<string, List<ModMapMarker>> oldMarkers)
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
    }
}
