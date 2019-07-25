// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Kingmaker.Utility;

namespace CustomMapMarkers
{
    class StateManager
    {
        [Serializable]
        public class SavedState
        {
            public readonly uint Version = 1;   // Data version for serialization
            public Dictionary<string, List<ModMapMarker>> AreaMarkers { get; private set; }
            public uint MarkerNumber = 1;       // Used in creating marker names

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
        private static string SavedStateFile = "custom-map-markers.bin";

        public static void LoadState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
            if (File.Exists(stateFile) && new FileInfo(stateFile).Length > 0)
            {
                using (FileStream fs = new FileStream(stateFile, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    CurrentState = (SavedState) formatter.Deserialize(fs);
                    fs.Close();
                }
            }
            else
            {
                CurrentState = new SavedState();
            }
        }

        public static void SaveState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
            using (FileStream writer = new FileStream(stateFile, FileMode.Create))
            {
                var savedState = CurrentState.CleanCopyForSave();
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(writer, savedState);
                writer.Close();
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
