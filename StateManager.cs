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
        public static Dictionary<string, List<ModMapMark>> AllMarks { get; private set; }
        private static string SavedStateFile = "custom-map-markers.bin";

        public static void LoadState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
            if (File.Exists(stateFile) && new FileInfo(stateFile).Length > 0)
            {
                using (FileStream fs = new FileStream(stateFile, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    var loadedMarks = (Dictionary<string, List<ModMapMark>>) formatter.Deserialize(fs);
                    AllMarks = PurgeDeleted(loadedMarks);
                    fs.Close();
                }
            }
            else
            {
                AllMarks = new Dictionary<string, List<ModMapMark>>();
            }
        }

        public static void SaveState()
        {
            string stateFile = Path.Combine(ApplicationPaths.persistentDataPath, SavedStateFile);
            using (FileStream writer = new FileStream(stateFile, FileMode.Create))
            {
                Dictionary<string, List<ModMapMark>> savedMarks = PurgeDeleted(AllMarks);
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(writer, savedMarks);
                writer.Close();
            }
        }

        private static Dictionary<string, List<ModMapMark>> PurgeDeleted(Dictionary<string, List<ModMapMark>> oldMarks)
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
