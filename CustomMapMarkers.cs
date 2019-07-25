// Copyright (c) 2019 v1ld.git@gmail.com
// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.ServiceWindow.LocalMap;
using Kingmaker.Visual.LocalMap;
using UnityEngine;
using UnityEngine.EventSystems;


namespace CustomMapMarkers
{
    class CustomMapMarkers : ISceneHandler, IWarningNotificationUIHandler
    {
        private static Dictionary<string, List<ModMapMark>> AllMarks { get { return StateManager.AllMarks; } }
        private static bool IsFirstTimeLocalMapShown = false;

        internal static void Load()
        {
            EventBus.Subscribe(new CustomMapMarkers());
            StateManager.LoadState();
        }

        internal static void FirstTimeShowLocalMap()
        {
            if (IsFirstTimeLocalMapShown) { return; }
            AddMarkstoLocalMap();
            IsFirstTimeLocalMapShown = true;
        }

        private static FastInvoke LocalMap_Set = Helpers.CreateInvoker<LocalMap>("Set");

        public static void CreateMark(LocalMap map, PointerEventData eventData)
        {
            ModMapMark mark = NewMark(map, eventData);
            LocalMap.Markers.Add(mark);
            LocalMap_Set(map);  // Force a refresh to display the new mark
            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
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
            List<ModMapMark> marks;
            if (AllMarks.TryGetValue(areaName, out marks)) {
                foreach (var mark in marks)
                {
                    LocalMap.Markers.Add(mark);
                }
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
            StateManager.SaveState();
            RemoveMarksFromLocalMap();
        }

        void IWarningNotificationUIHandler.HandleWarning(WarningNotificationType warningType, bool addToLog)
        {
            switch (warningType)
            {
                case WarningNotificationType.GameSaved:
                case WarningNotificationType.GameSavedAuto:
                case WarningNotificationType.GameSavedQuick:
                    StateManager.SaveState();
                    break;
            }
        }

        void IWarningNotificationUIHandler.HandleWarning(string text, bool addToLog) { }
    }

    [Serializable]
    class ModMapMark :  ILocalMapMarker
	{
        public string Description { get; set; }
        private SerializableVector3 Position;
        public LocalMap.MarkType Type { get; set; }
        public bool IsVisible { get; set; } = true;

        [NonSerialized] public bool IsDeleted = false;
        [NonSerialized] public bool IsBeingDeleted = false;
        [NonSerialized] private static uint MarkerNumber = 1;

        public ModMapMark(Vector3 position)
        {
            Description = $"Custom marker #{MarkerNumber++}";
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
            => IsVisible;
    }

    class CustomMapMarkersMenu {
        private static Dictionary<string, List<ModMapMark>> AllMarks { get { return StateManager.AllMarks; } }
        internal static int lastAreaMenu = 0;

        internal static void Layout()
        {
            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };
            if (AllMarks.Count == 0)
            {
                GUILayout.Label("<b>No custom markers.</b>", fixedWidth);
                return;
            }

            string[] areaNames = AllMarks.Keys.ToArray();
            Array.Sort(areaNames);

            GUILayout.Label("<b>Select area</b>", fixedWidth);
            lastAreaMenu = GUILayout.SelectionGrid(lastAreaMenu, areaNames, 10, fixedWidth);
            GUILayout.Space(10f);
            GUILayout.Label($"<b>{areaNames[lastAreaMenu]}</b>", fixedWidth);
            LayoutMarkersForArea(areaNames[lastAreaMenu]);
        }

        private static void LayoutMarkersForArea(string areaName)
        {
            string[] types = { "Point of Interest", "Very Important Thing" };
            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };

            uint i = 1;
            foreach (var mark in AllMarks[areaName])
            {
                if (mark.IsDeleted) { continue; }

                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                GUILayout.Label(mark.IsVisible ? $"<b>{i++}: {mark.Description}</b>" : $"{i++}: {mark.Description}", fixedWidth);
                if (GUILayout.Button(mark.IsVisible ? "Hide" : "Show", fixedWidth))
                {
                    mark.IsVisible = !mark.IsVisible;
                }
                if (!mark.IsBeingDeleted && GUILayout.Button("Delete", fixedWidth))
                {
                    mark.IsBeingDeleted = true;
                }
                if (mark.IsBeingDeleted)
                {
                    GUILayout.Label("Are you sure?", fixedWidth);
                    if (GUILayout.Button("Yes", fixedWidth))
                    {
                        LocalMap.Markers.Remove(mark);
                        mark.IsDeleted = true;
                        mark.IsVisible = false;
                    }
                    if (GUILayout.Button("No", fixedWidth))
                    {
                        mark.IsBeingDeleted = false;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Description: ", fixedWidth);
                mark.Description = GUILayout.TextField(mark.Description, GUILayout.MaxWidth(250f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Type: ", fixedWidth);
                int typeIndex = GUILayout.SelectionGrid(mark.Type == LocalMap.MarkType.Poi ? 0 : 1, types, types.Length, fixedWidth);
                mark.Type = typeIndex == 0 ? LocalMap.MarkType.Poi : LocalMap.MarkType.VeryImportantThing;
                GUILayout.EndHorizontal();
            }
        }
    }
}
