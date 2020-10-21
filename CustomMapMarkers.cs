// Copyright (c) 2019 v1ld.git@gmail.com
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Kingmaker;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.ServiceWindow.LocalMap;
using Kingmaker.Visual.LocalMap;
using UnityEngine;
using UnityEngine.EventSystems;


namespace CustomMapMarkers
{
    class CustomMapMarkers
    {
        private static Dictionary<string, List<ModMapMarker>> AreaMarkers { get { return StateManager.CurrentState.AreaMarkers; } }

        internal static void Load()
        {
            EventBus.Subscribe(new CustomMapMarkers());
        }

        internal static void OnShowLocalMap()
        {
            AddMarkerstoLocalMap();
        }

        private static FastInvoke LocalMap_Set = Helpers.CreateInvoker<LocalMap>("Set");

        internal static void CreateMarker(LocalMap map, PointerEventData eventData)
        {
            ModMapMarker marker = NewMarker(map, eventData);
            LocalMapModel.Markers.Add(marker);
            LocalMap_Set(map);  // Force a refresh to display the new mark
            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
        }

        private static ModMapMarker NewMarker(LocalMap map, PointerEventData eventData)
        {
            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            List <ModMapMarker> markersForArea;
            if (!AreaMarkers.TryGetValue(areaName, out markersForArea)) { AreaMarkers[areaName] = new List<ModMapMarker>(); }
            Vector3 position = GetPositionFromEvent(map, eventData);
            ModMapMarker marker = new ModMapMarker(position);
            AreaMarkers[areaName].Add(marker);
            return marker;
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

        internal static void AddMarkerstoLocalMap()
        {
            if (StateManager.CurrentState.IsLocalMapInitialized) { return; }

#if DEBUG
            foreach (var marker in LocalMapModel.Markers)
            {
                if (marker is ModMapMarker)
                {
                    Log.Write($"AddMarkerstoLocalMap: ModMapMarker present before add! marker=[{((ModMapMarker)marker).Description}]");
                    // LocalMap.Markers.Remove(marker);
                }
            }
#endif

            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            List<ModMapMarker> markers;
            if (AreaMarkers.TryGetValue(areaName, out markers))
            {
                foreach (var marker in markers)
                {
                    Log.Write($"AddMarkerstoLocalMap: marker=[{marker.Description}]");
                    LocalMapModel.Markers.Add(marker);
                }
            }

            StateManager.CurrentState.IsLocalMapInitialized = true;
        }

        internal static void RemoveMarkersFromLocalMap()
        {
            string areaName = Game.Instance.CurrentlyLoadedArea.AreaDisplayName;
            List<ModMapMarker> markers;
            if (AreaMarkers.TryGetValue(areaName, out markers))
            {
                foreach (var marker in markers)
                {
                    Log.Write($"RemoveMarkersFromLocalMap: marker=[{((ModMapMarker)marker).Description}]");
                    LocalMapModel.Markers.Remove(marker);
                }
            }

            StateManager.CurrentState.IsLocalMapInitialized = false;
        }
    }

    [DataContract]
    class ModMapMarker :  ILocalMapMarker
	{
        [DataMember]
        internal string Description;
        [DataMember]
        private SerializableVector3 Position;
        [DataMember]
        internal LocalMap.MarkType Type;
        [DataMember]
        internal bool IsVisible = true;

        internal bool IsDeleted = false;
        internal bool IsBeingDeleted = false;

        internal ModMapMarker(Vector3 position)
        {
            Description = $"Custom marker #{StateManager.CurrentState.MarkerNumber++}";
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
        private static Dictionary<string, List<ModMapMarker>> AreaMarkers { get { return StateManager.CurrentState.AreaMarkers; } }
        private static int lastAreaMenu = 0;
        private static string[] MarkTypeNames = { "Point of Interest", "Very Important Thing", "Loot", "Exit" };
        private static LocalMap.MarkType[] MarkTypes = { LocalMap.MarkType.Poi, LocalMap.MarkType.VeryImportantThing, LocalMap.MarkType.Loot, LocalMap.MarkType.Exit };


        internal static void Layout()
        {
            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };
            if (AreaMarkers.Count == 0)
            {
                GUILayout.Label("<b>No custom markers.</b>", fixedWidth);
                return;
            }

            string[] areaNames = AreaMarkers.Keys.ToArray();
            Array.Sort(areaNames);
            lastAreaMenu = (lastAreaMenu >= areaNames.Length) ? 0 : lastAreaMenu;

            GUILayout.Label("<b><color=cyan>Select area</color></b>", fixedWidth);
            lastAreaMenu = GUILayout.SelectionGrid(lastAreaMenu, areaNames, 5, fixedWidth);
            GUILayout.Space(10f);
            GUILayout.Label($"<b><color=cyan>{areaNames[lastAreaMenu]}</color></b>", fixedWidth);
            LayoutMarkersForArea(areaNames[lastAreaMenu]);
        }

        private static void LayoutMarkersForArea(string areaName)
        {
            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };

            uint markerNumber = 1;
            foreach (var marker in AreaMarkers[areaName])
            {
                if (marker.IsDeleted) { continue; }

                GUILayout.Space(10f);

                string markerLabel = $"{markerNumber++}: {marker.Description}";
                if (marker.IsVisible) { markerLabel = $"<color=#1aff1a><b>{markerLabel}</b></color>"; }
                GUILayout.Label(markerLabel, fixedWidth);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Description: ", fixedWidth);
                marker.Description = GUILayout.TextField(marker.Description, GUILayout.MaxWidth(500f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Type: ", fixedWidth);
                for (int i = 0; i < MarkTypeNames.Length; i++)
                {
                    if (GUILayout.Toggle(marker.Type == MarkTypes[i], MarkTypeNames[i], fixedWidth))
                    {
                        marker.Type = MarkTypes[i];
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(marker.IsVisible ? "Hide" : "Show", fixedWidth))
                {
                    marker.IsVisible = !marker.IsVisible;
                }
                if (!marker.IsBeingDeleted && GUILayout.Button("Delete", fixedWidth))
                {
                    marker.IsBeingDeleted = true;
                }
                if (marker.IsBeingDeleted)
                {
                    GUILayout.Label("Are you sure?", fixedWidth);
                    if (GUILayout.Button("Yes", fixedWidth))
                    {
                        LocalMapModel.Markers.Remove(marker);
                        marker.IsDeleted = true;
                        marker.IsVisible = false;
                    }
                    if (GUILayout.Button("No", fixedWidth))
                    {
                        marker.IsBeingDeleted = false;
                    }
                }
                GUILayout.EndHorizontal();
            } 
        }
    }
}
