﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using QFramework.CodeGen;
using Invert.Data;
using UnityEngine;

namespace QFramework.CodeGen
{
    public class DiagramViewModel : ViewModel, IDataRecordInserted, IDataRecordRemoved, IDataRecordPropertyChanged
    {
        private List<GraphItemViewModel> _graphItems = new List<GraphItemViewModel>();


        private InspectorViewModel _inspectorViewModel;
        private GraphDesignerNavigationViewModel _navigationViewModel;


        public IEnumerable<GraphItemViewModel> AllViewModels
        {
            get
            {
                foreach (var item in GraphItems)
                {
                    foreach (var child in item.ContentItems)
                    {
                        yield return child;
                    }

                    yield return item;
                }
            }
        }


        public IEnumerable<GraphItemViewModel> SelectedNodeItems
        {
            get
            {
                return GraphItems.OfType<DiagramNodeViewModel>().SelectMany(p => p.ContentItems)
                    .Where(p => p.IsSelected);
            }
        }

        public IGraphData GraphData { get; set; }


        public IRepository CurrentRepository { get; set; }

        public DiagramViewModel(IGraphData diagram)
        {
            if (diagram == null) throw new Exception("Diagram not found");
            CurrentRepository = diagram.Repository;
        }

        public Dictionary<string, IFilterItem> FilterItems { get; set; }




        public IEnumerator AddGraphItems(IEnumerable<IDiagramNode> items)
        {
            var dictionary = new Dictionary<string, IFilterItem>();
            foreach (var item in GraphData.CurrentFilter.FilterItems)
            {
                if (dictionary.ContainsKey(item.NodeId))
                {
                    item.Repository.Remove(item);
                    continue;
                }

                dictionary.Add(item.NodeId, item);
            }

            FilterItems = dictionary;


            IsLoading = true;
            foreach (var item in items)
            {
                var mapping =
                    InvertApplication.Container.RelationshipMappings[item.GetType(), typeof(INotifyPropertyChanged)];
                if (mapping == null) continue;
                var vm = Activator.CreateInstance(mapping, item, this) as GraphItemViewModel;
                if (vm == null)
                {
                    continue;
                }

                vm.DiagramViewModel = this;
                GraphItems.Add(vm);
                yield return new TaskProgress(string.Format("Loading..."), 95f);
            }

            IsLoading = false;
            RefreshConnectors();
            yield break;
        }

        public InspectorViewModel InspectorViewModel
        {
            get
            {
                return _inspectorViewModel ?? (_inspectorViewModel = new InspectorViewModel()
                {
                    DiagramViewModel = this
                });
            }
            set { _inspectorViewModel = value; }
        }

        public GraphDesignerNavigationViewModel NavigationViewModel
        {
            get
            {
                return _navigationViewModel ?? (_navigationViewModel = new GraphDesignerNavigationViewModel()
                {
                    DiagramViewModel = this
                });
            }
            set { _navigationViewModel = value; }
        }

        //public void RefreshConnectors()
        //{

        //    var items = GraphItems.OfType<ConnectorViewModel>().ToArray();
        //    var connections = GraphItems.OfType<ConnectionViewModel>().ToArray();

        //    foreach (var item in items)
        //    {
        //        GraphItems.Remove(item);
        //    }
        //    foreach (var item in connections)
        //    {
        //        GraphItems.Remove(item);
        //    }
        //    var connectors = new List<ConnectorViewModel>();
        //    foreach (var item in GraphItems)
        //    {
        //        item.GetConnectors(connectors);
        //    }
        //    AddConnectors(connectors);
        //}

        public void RefreshConnectors()
        {
        }

        public IDiagramNode[] CurrentNodes { get; set; }

        public List<GraphItemViewModel> GraphItems
        {
            get { return _graphItems; }
            set { _graphItems = value; }
        }

        public void DeselectAll()
        {
            if (InspectorViewModel != null)
            {
                InspectorViewModel.TargetViewModel = null;
            }

            foreach (var item in AllViewModels.ToArray())
            {
                var ivm = item as ItemViewModel;
                if (ivm != null)
                {
                    if (ivm.IsEditing)
                    {
                        ivm.EndEditing();
                        break;
                    }
                }

                var nvm = item as DiagramNodeViewModel;
                if (nvm != null)
                {
                    if (nvm.IsEditing)
                    {
                        nvm.EndEditing();
                        break;
                    }
                }


                if (item.IsSelected)
                    item.IsSelected = false;
            }


            InvertApplication.SignalEvent<INothingSelectedEvent>(_ => _.NothingSelected());
#if UNITY_EDITOR
            UnityEngine.GUI.FocusControl("");
#endif
        }

        //public void UpgradeProject()
        //{
        //    uFrameEditor.ExecuteCommand(new ConvertToJSON());
        //}

        public void NothingSelected()
        {
            var items = SelectedNodeItems.OfType<ItemViewModel>().Where(p => p.IsEditing).ToArray();
            if (items.Length > 0)
            {
            }

            DeselectAll();

            //InvertGraphEditor.ExecuteCommand(_ => { });
        }

        public void Select(GraphItemViewModel viewModelObject)
        {
            if (viewModelObject == null) return;

            InspectorViewModel.TargetViewModel = viewModelObject;

            if (viewModelObject.IsSelected)
            {
                return;
            }

            viewModelObject.IsSelected = true;
            InvertApplication.SignalEvent<IGraphSelectionEvents>(
                _ => _.SelectionChanged(viewModelObject));
        }


        //public void UpgradeProject()
        //{
        //    InvertApplication
        //    InvertGraphEditor.ExecuteCommand((n) =>
        //    {
        //        Process15Uprade();
        //    });

        //}

        //public void Process15Uprade()
        //{

        //}


        public FilterItem AddNode(IDiagramNode newNodeData, Vector2 position)
        {
            newNodeData.GraphId = GraphData.Identifier;
            CurrentRepository.Add(newNodeData);

            if (string.IsNullOrEmpty(newNodeData.Name))
                newNodeData.Name =
                    CurrentRepository.GetUniqueName("New" + newNodeData.GetType().Name.Replace("Data", ""));

            return GraphData.CurrentFilter.ShowInFilter(newNodeData, position);
        }

        public bool IsLoading { get; set; }


        public void RecordInserted(IDataRecord record)
        {
            var filterItem = record as IFilterItem;
            if (filterItem != null)
            {
                if (filterItem.FilterId == GraphData.CurrentFilter.Identifier)
                {
                    var e = AddGraphItems(new[] {filterItem.Node});
                    while (e.MoveNext())
                    {
                    }
                }
            }


            for (int index = 0; index < GraphItems.Count; index++)
            {
                var item = GraphItems[index];
                item.RecordInserted(record);
            }
        }

     

        public void PropertyChanged(IDataRecord record, string name, object previousValue, object nextValue)
        {
            //if (record == GraphData)
            //{
            //    Load(true);
            //    return;
            //}
            for (int index = 0; index < GraphItems.Count; index++)
            {
                var item = GraphItems[index];
                item.PropertyChanged(record, name, previousValue, nextValue);
            }
        }
    }
}