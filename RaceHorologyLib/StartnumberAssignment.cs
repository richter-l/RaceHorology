﻿/*
 *  Copyright (C) 2019 - 2020 by Sven Flossmann
 *  
 *  This file is part of Race Horology.
 *
 *  Race Horology is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Affero General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  any later version.
 * 
 *  Race Horology is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Affero General Public License for more details.
 *
 *  You should have received a copy of the GNU Affero General Public License
 *  along with Race Horology.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  Diese Datei ist Teil von Race Horology.
 *
 *  Race Horology ist Freie Software: Sie können es unter den Bedingungen
 *  der GNU Affero General Public License, wie von der Free Software Foundation,
 *  Version 3 der Lizenz oder (nach Ihrer Wahl) jeder neueren
 *  veröffentlichten Version, weiter verteilen und/oder modifizieren.
 *
 *  Race Horology wird in der Hoffnung, dass es nützlich sein wird, aber
 *  OHNE JEDE GEWÄHRLEISTUNG, bereitgestellt; sogar ohne die implizite
 *  Gewährleistung der MARKTFÄHIGKEIT oder EIGNUNG FÜR EINEN BESTIMMTEN ZWECK.
 *  Siehe die GNU Affero General Public License für weitere Details.
 *
 *  Sie sollten eine Kopie der GNU Affero General Public License zusammen mit diesem
 *  Programm erhalten haben. Wenn nicht, siehe <https://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RaceHorologyLib
{

  public class AssignedStartNumber : INotifyPropertyChanged
  {
    private uint _startNumber;
    private RaceParticipant _particpant;

    public uint StartNumber { get { return _startNumber; } set { _startNumber = value; NotifyPropertyChanged(); } }
    public RaceParticipant Participant { get { return _particpant; } set { _particpant = value; NotifyPropertyChanged(); } }

    #region INotifyPropertyChanged implementation

    public event PropertyChangedEventHandler PropertyChanged;
    // This method is called by the Set accessor of each property.  
    // The CallerMemberName attribute that is applied to the optional propertyName  
    // parameter causes the property name of the caller to be substituted as an argument.  
    private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Pass through the property change
    private void OnParticipantPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      NotifyPropertyChanged(args.PropertyName);
    }

    #endregion
  }

  /// <summary>
  /// Represents the startnumber asignments and its working space
  /// </summary>
  public class StartNumberAssignment
  {
    ObservableCollection<AssignedStartNumber> _snAssignment;
    private uint _nextFreeStartNumber;
    private List<uint> _snsNotToAssign;


    public StartNumberAssignment()
    {
      _snAssignment = new ObservableCollection<AssignedStartNumber>();

      _snsNotToAssign = new List<uint>();

      determineNextFreeStartNumber();
    }


    public event EventHandler NextStartnumberChanged;
    public uint NextFreeStartNumber
    { 
      get { return _nextFreeStartNumber; } 

      private set 
      { 
        if (_nextFreeStartNumber != value)
        {
          _nextFreeStartNumber = value;

          var handler = NextStartnumberChanged;
          if (handler != null)
            handler.Invoke(this, new EventArgs());
        }
      }
    }


    public void SetStartNumbersNotToAssign(IEnumerable<uint> sns)
    {
      _snsNotToAssign.Clear();
      _snsNotToAssign.AddRange(sns);
    }


    public void LoadFromRace(Race race)
    {
      var particpants = race.GetParticipants();

      foreach(var p in particpants)
      {
        if (p.StartNumber != 0)
          Assign(p.StartNumber, p);
      }
    }


    /// <summary>
    /// Save the startnumber assignment to the race
    /// In case a participant has a startnumber, but is not assigned in this workspace, the startnumber will be deleted.
    /// </summary>
    /// <param name="race"></param>
    public void SaveToRace(Race race)
    {
      var particpants = race.GetParticipants();

      foreach (var p in particpants)
      {
        var ass = _snAssignment.FirstOrDefault(a => a.Participant == p);
        if (ass != null)
        {
          p.StartNumber = ass.StartNumber;
        }
        else
        {
          p.StartNumber = 0;
        }
      }
    }


    /// <summary>
    /// Returns the workng space containing current StartNumber and Particpant
    /// </summary>
    public ObservableCollection<AssignedStartNumber> ParticipantList
    { get { return _snAssignment; } }

    /// <summary>
    /// Assigns next free startnumber to the participant
    /// </summary>
    /// <param name="participant">The particpant the startnumber to assign</param>
    /// <returns></returns>
    public uint AssignNextFree(RaceParticipant participant)
    {
      uint sn = NextFreeStartNumber;
      Assign(sn, participant);
      return sn;
    }

    /// <summary>
    /// Assignes the specified startnumber to the specified participant
    /// </summary>
    /// <param name="sn">The start number</param>
    /// <param name="participant">The particpant the startnumber to assign</param>
    public void Assign(uint sn, RaceParticipant participant)
    {
      int updateSNFrom = Math.Min(_snAssignment.Count, (int)sn - 1);

      // Check if already existing and assign null
      var pAlreadyExisting = _snAssignment.FirstOrDefault(v => v.Participant == participant);
      if (pAlreadyExisting!=null)
      {
        pAlreadyExisting.Participant = null;
      }


      if ((int)sn - 1 < _snAssignment.Count)
        _snAssignment[(int)sn - 1] = new AssignedStartNumber { Participant = participant };
      else
      {
        // Fill up with empty space
        while (_snAssignment.Count < (int)sn - 1)
          _snAssignment.Add(new AssignedStartNumber());

        // Put participant at the correct place
        _snAssignment.Add(new AssignedStartNumber { Participant = participant });
      }

      updateStartNumbers(updateSNFrom);
    }


    public bool IsAssigned(RaceParticipant rp)
    {
      var pAlreadyExisting = _snAssignment.FirstOrDefault(v => v.Participant == rp);
      return pAlreadyExisting != null;
    }

    /// <summary>
    /// Inserts a new startnumber slot at the given position. All existing start numbers higher that sn will be increased accordingly.
    /// </summary>
    /// <param name="sn"></param>
    public void InsertAndShift(uint sn)
    {
      _snAssignment.Insert((int)sn-1, new AssignedStartNumber());

      updateStartNumbers((int)sn - 1);
    }

    /// <summary>
    /// Removes a new startnumber slot at the given position. All existing start numbers higher that sn will be descreased accordingly.
    /// </summary>
    /// <param name="sn"></param>
    public void Delete(uint sn)
    {
      if ( sn - 1 < _snAssignment.Count)
        _snAssignment.RemoveAt((int)sn - 1);

      updateStartNumbers((int)sn - 1);
    }


    /// <summary>
    /// Delete all assignments
    /// </summary>
    public void DeleteAll()
    {
      _snAssignment.Clear();
      determineNextFreeStartNumber();
    }

    /// <summary>
    /// Returns the next free startnumber (number of assigned startnumber slots + 1)
    /// </summary>
    /// <returns></returns>
    private void determineNextFreeStartNumber()
    {
      uint sn = 0;
      if (_snAssignment.Count == 0)
        sn = 1;
      else
        sn = _snAssignment.Last().StartNumber + 1U;

      while (_snsNotToAssign.Contains(sn))
        sn++;

      NextFreeStartNumber = sn;
    }

    /// <summary>
    /// Internal function to update the startnumber for each slot correctly starting from from.
    /// </summary>
    /// <param name="from">Update starts at this startnumber (optional, just an optimization)</param>
    protected void updateStartNumbers(int from = 0)
    {
      for (int i = from; i < _snAssignment.Count; i++)
        if (_snAssignment != null )//&& _snAssignment[i] != null)
          _snAssignment[i].StartNumber = (uint)i + 1;

      determineNextFreeStartNumber();
    }
  }


  public class ParticpantSelector
  {
    private Race _race;
    private StartNumberAssignment _snAssignment;
    private string _groupProperty;

    private object _currentGroup;

    private Random _random;

    private Dictionary<object, List<RaceParticipant>> _group2participant;


    public event EventHandler CurrentGroupChanged;
    public event EventHandler GroupingChanged;

    public ParticpantSelector(Race race, StartNumberAssignment snAssignment, string groupProperty = null)
    {
      _race = race;
      _snAssignment = snAssignment;
      _groupProperty = groupProperty;

      _currentGroup = null;

      _random = new Random();

      _group2participant = new Dictionary<object, List<RaceParticipant>>();
      fillGroup2Particpant();
    }


    public Dictionary<object, List<RaceParticipant>> Group2Participant { get { return _group2participant; } private set { _group2participant = value; } }

    public string GroupProperty
    {
      get { return _groupProperty; }
      set 
      {
        if (_groupProperty != value)
        {
          _groupProperty = value;
          fillGroup2Particpant();
        }
      }
    }


    private void fillGroup2Particpant()
    {
      _group2participant.Clear();

      foreach (var rp in _race.GetParticipants())
      {
        object group = "";
        if (_groupProperty != null)
          group = PropertyUtilities.GetPropertyValue(rp, _groupProperty);

        if (!_group2participant.ContainsKey(group))
          _group2participant[group] = new List<RaceParticipant>();

        _group2participant[group].Add(rp);
      }

      var handler = GroupingChanged;
      if (handler != null)
        handler.Invoke(this, new EventArgs());

      SwitchToFirstGroup();
    }


    public object CurrentGroup
    {
      get { return _currentGroup; }
      private set 
      {
        if (_currentGroup != value)
        {
          _currentGroup = value;

          var handler = CurrentGroupChanged;
          if (handler != null)
            handler.Invoke(this, new EventArgs());
        }
      }
    }


    public bool SwitchToFirstGroup()
    {
      List<object> groups = _group2participant.Keys.ToList();
      groups.Sort();

      if (groups.Count > 0)
        CurrentGroup = groups[0];
      else
        CurrentGroup = null;

      return CurrentGroup != null;
    }


    public bool SwitchToNextGroup()
    {
      List<object> groups = _group2participant.Keys.ToList();
      groups.Sort();

      int index = int.MaxValue;
      if (CurrentGroup != null)
      {
        index = groups.FindIndex(g => g == _currentGroup);
        index++;
      }

      if (index >= groups.Count)
        CurrentGroup = null;
      else
        CurrentGroup = groups[index];

      return CurrentGroup != null;
    }


    public void AssignParticipants()
    {
      AssignParticipants(_currentGroup);
    }


    public void AssignParticipants(object group)
    {
      if (group != null && _group2participant.ContainsKey(group))
        AssignParticipants(_group2participant[group]);
    }


    public void AssignParticipants(List<RaceParticipant> participants)
    {
      var wcParticipants = participants.ToList();

      while (wcParticipants.Count > 0)
      {
        RaceParticipant rp = pickParticipant(wcParticipants);
        assignParticipant(rp);
        removeParticipant(wcParticipants, rp);
      }
    }


    protected void assignParticipant(RaceParticipant rp)
    {
      if (!_snAssignment.IsAssigned(rp))
        _snAssignment.AssignNextFree(rp);
    }


    protected RaceParticipant pickParticipant(List<RaceParticipant> participants)
    {
      int pickedIndex = _random.Next(participants.Count);
      return participants[pickedIndex];
    }

    protected void removeParticipant(List<RaceParticipant> participants, RaceParticipant rp)
    {
      participants.Remove(rp);
    }


  }
}
  