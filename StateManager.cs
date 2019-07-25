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
            public Dictionary<string, List<ModMapMark>> AreaMarks { get; private set; }
            public uint MarkerNumber = 1;       // Used in creating marker names

            public SavedState()
            {
                AreaMarks = new Dictionary<string, List<ModMapMark>>();
            }

            public SavedState(Dictionary<string, List<ModMapMark>> marks)
            {
                AreaMarks = marks;
            }

            public SavedState CleanCopyForSave()
            {
                var clone = (SavedState)this.MemberwiseClone();
                clone.AreaMarks = StateHelpers.PurgeDeletedMarks(this.AreaMarks);
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
        public static Dictionary<string, List<ModMapMark>> PurgeDeletedMarks(Dictionary<string, List<ModMapMark>> oldMarks)
        {
            Dictionary<string, List<ModMapMark>> newMarks = new Dictionary<string, List<ModMapMark>>();
            foreach (var area in oldMarks.Keys)
            {
                if (oldMarks[area].Any(mark => mark.IsDeleted))
                {
                    List<ModMapMark> marks = oldMarks[area].FindAll(mark => !mark.IsDeleted);
                    if (marks.Count() > 0)
                    {
                        newMarks[area] = marks;
                    }
                }
                else
                {
                    newMarks[area] = oldMarks[area];
                }
            }
            return newMarks;
        }
    }
}
