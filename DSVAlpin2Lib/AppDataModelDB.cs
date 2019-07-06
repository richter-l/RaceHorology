﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSVAlpin2Lib
{
  /// <summary>
  /// Observes the run results and triggers a database store in case time / run results changed
  /// </summary>
  /// <remarks>
  /// Delete not implemented (actually not needed)
  /// </remarks>
  internal class DatabaseDelegatorRaceRun
  {
    private RaceRun _rr;
    private IAppDataModelDataBase _db;

    public DatabaseDelegatorRaceRun(RaceRun rr, IAppDataModelDataBase db)
    {
      _db = db;
      _rr = rr;

      rr.GetResultList().ItemChanged += OnItemChanged;
      rr.GetResultList().CollectionChanged += OnCollectionChanged;
    }

    private void OnItemChanged(object sender, PropertyChangedEventArgs e)
    {
      RunResult result = (RunResult)sender;
      _db.CreateOrUpdateRunResult(_rr, result);
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Add:
          foreach (RunResult v in e.NewItems)
            _db.CreateOrUpdateRunResult(_rr, v);
          break;

        case NotifyCollectionChangedAction.Move:
        case NotifyCollectionChangedAction.Remove:
        case NotifyCollectionChangedAction.Replace:
        case NotifyCollectionChangedAction.Reset:
          throw new Exception("not implemented");
      }
    }
  }

  /// <summary>
  /// Observes the Patients and triggers a database store if needed
  /// </summary>
  /// <remarks>
  /// Delete not yet implemented
  /// </remarks>
  internal class DatabaseDelegatorParticipant
  {
    private ItemsChangeObservableCollection<Participant> _participants;
    private IAppDataModelDataBase _db;

    public DatabaseDelegatorParticipant(ItemsChangeObservableCollection<Participant> participants, IAppDataModelDataBase db)
    {
      _db = db;
      _participants = participants;

      _participants.ItemChanged += OnItemChanged;
      _participants.CollectionChanged += OnCollectionChanged;
    }

    private void OnItemChanged(object sender, PropertyChangedEventArgs e)
    {
      Participant participant = (Participant)sender;
      _db.CreateOrUpdateParticipant(participant);
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      switch (e.Action)
      {
        case NotifyCollectionChangedAction.Add:
          foreach (Participant participant in e.NewItems)
            _db.CreateOrUpdateParticipant(participant);
          break;

        case NotifyCollectionChangedAction.Move:
        case NotifyCollectionChangedAction.Remove:
        case NotifyCollectionChangedAction.Replace:
        case NotifyCollectionChangedAction.Reset:
          throw new Exception("not implemented");
      }
    }
  }


}