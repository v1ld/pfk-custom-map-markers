// Copyright (c) 2019 v1ld.git@gmail.com
// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Kingmaker;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.ServiceWindow.LocalMap;
using Kingmaker.Utility;
using Kingmaker.Visual.LocalMap;
using UnityEngine;
using UnityEngine.EventSystems;


namespace CustomMapMarkers
{
    class CustomMapMarkers : ISceneHandler
    {
        public static Dictionary<string, List<ModMapMark>> AllMarks { get; private set; }
        private static bool IsInitialized = false;
        private static string MapMarkersFile = "custom-map-markers.bin";
        internal static uint markerNumber = 1;

        internal static void Load()
        {
            EventBus.Subscribe(new CustomMapMarkers());
        }

        internal static void Initialize()
        {
            if (IsInitialized) { return; }
            LoadFromFile(true);
            IsInitialized = true;
        }

        private static void LoadFromFile(bool addMarkstoMap)
        {
            string markerFile = Path.Combine(ApplicationPaths.persistentDataPath, MapMarkersFile);
            if (File.Exists(markerFile))
            {
                using (FileStream fs = new FileStream(markerFile, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    AllMarks = (Dictionary<string, List<ModMapMark>>) formatter.Deserialize(fs);
                    fs.Close();
                    if (addMarkstoMap)
                    {
                        AddMarkstoLocalMap();
                    }
                }
            }
            else
            {
                AllMarks = new Dictionary<string, List<ModMapMark>>();
            }
        }

        private static void SaveToFile()
        {
            string markerFile = Path.Combine(ApplicationPaths.persistentDataPath, MapMarkersFile);
            using (FileStream writer = new FileStream(markerFile, FileMode.Create))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(writer, AllMarks);
                writer.Close();
            }
        }

        private static FastInvoke LocalMap_Set = Helpers.CreateInvoker<LocalMap>("Set");

        public static void CreateMark(LocalMap map, PointerEventData eventData)
        {
            ModMapMark mark = NewMark(map, eventData);
            LocalMap.Markers.Add(mark);
            LocalMap_Set(map);  // Force a refresh to display the new mark
            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
            SaveToFile();
        }

        private static ModMapMark NewMark(LocalMap map, PointerEventData eventData)
        {
            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            List <ModMapMark> marksForArea;
            if (!AllMarks.TryGetValue(areaName, out marksForArea)) { AllMarks[areaName] = new List<ModMapMark>(); }
            Vector3 position = GetPositionFromEvent(map, eventData);
            ModMapMark mark = new ModMapMark(position);
            AllMarks[areaName].Add(mark);
            return mark;
        }

        private static Vector3 GetPositionFromEvent(LocalMap map, PointerEventData eventData)
        {
            Vector2 vector2;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(map.Image.rectTransform, eventData.position, Game.Instance.UI.UICamera, out vector2);
			vector2 += Vector2.Scale(map.Image.rectTransform.sizeDelta, map.Image.rectTransform.pivot);
            LocalMapRenderer.DrawResult drawResult = Helpers.GetField<LocalMapRenderer.DrawResult>(map, "m_DrawResult");
			Vector2 vector21 = new Vector2(vector2.x / (float)drawResult.ColorRT.width, vector2.y / (float)drawResult.ColorRT.height);
			Vector3 worldPoint = LocalMapRenderer.Instance.ViewportToWorldPoint(vector21);
            return worldPoint;
        }

        internal static void AddMarkstoLocalMap()
        {
            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            foreach (var mark in AllMarks[areaName])
            {
                LocalMap.Markers.Add(mark);
            }
        }

        internal static void RemoveMarksFromLocalMap()
        {
            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            foreach (var mark in AllMarks[areaName])
            {
                LocalMap.Markers.Remove(mark);
            }
        }

        void ISceneHandler.OnAreaDidLoad()
        {
            AddMarkstoLocalMap();
        }

        void ISceneHandler.OnAreaBeginUnloading()
        {
            RemoveMarksFromLocalMap();
        }
  }

    [Serializable()]
    class ModMapMark :  ILocalMapMarker
	{
        public string Description { get; set; }
        private SerializableVector3 Position;
        public LocalMap.MarkType Type { get; set; }

        public ModMapMark(Vector3 position)
        {
            Description = $"Custom marker #{CustomMapMarkers.markerNumber++}";
            Position = position;
            Type = LocalMap.MarkType.Poi;
        }

        string ILocalMapMarker.GetDescription()
            => Description;

        LocalMap.MarkType ILocalMapMarker.GetMarkerType()
            => Type;

        Vector3 ILocalMapMarker.GetPosition()
            => Position;

        bool ILocalMapMarker.IsVisible()
            => true;
    }
}
