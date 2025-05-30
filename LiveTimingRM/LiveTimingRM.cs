﻿/*
 *  Copyright (C) 2019 - 2024 by Sven Flossmann
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

using DocumentFormat.OpenXml.Spreadsheet;
using RaceHorologyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


internal class IsCompleteObserver : IDisposable
{
  RaceRun _previousRaceRun;
  RaceRun _raceRun;
  Action<RaceRun, RaceRun> _updateStartListFunc;
  bool disposedValue;

  public IsCompleteObserver(RaceRun previousRaceRun, RaceRun raceRun, Action<RaceRun, RaceRun> updateStartListFunc)
  {
    _previousRaceRun = previousRaceRun;
    _raceRun = raceRun;
    _updateStartListFunc = updateStartListFunc;

    _previousRaceRun.PropertyChanged += OnChanged;
  }

  private void OnChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (e.PropertyName == "IsComplete")
      _updateStartListFunc(_previousRaceRun, _raceRun);
  }

  #region IDisposable implementation
  protected virtual void Dispose(bool disposing)
  {
    if (!disposedValue)
    {
      if (disposing)
      {
        _previousRaceRun.PropertyChanged -= OnChanged;
      }

      disposedValue = true;
    }
  }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
  #endregion
}

public class LiveTimingDelegator : IDisposable
{
  Race _race;
  LiveTimingRM _liveTiming;

  List<IDisposable> _notifier;

  public LiveTimingDelegator(Race race, LiveTimingRM liveTiming)
  {
    _race = race;
    _liveTiming = liveTiming;

    _notifier = new List<IDisposable>();
    
    ObserveRace();
  }


  #region IDisposable Support
  private bool disposedValue = false; // To detect redundant calls

  protected virtual void Dispose(bool disposing)
  {
    if (!disposedValue)
    {
      if (disposing)
      {
        foreach (var n in _notifier)
          n.Dispose();
      }

      disposedValue = true;
    }
  }

  // This code added to correctly implement the disposable pattern.
  public void Dispose()
  {
    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    Dispose(true);
    // TODO: uncomment the following line if the finalizer is overridden above.
    // GC.SuppressFinalize(this);
  }
  #endregion


  private void ObserveRace()
  {
    ItemsChangedNotifier notifier = new ItemsChangedNotifier(_race.GetParticipants());
    notifier.CollectionChanged += (o, e) =>
    {
      _liveTiming.UpdateParticipants();
    };
    _liveTiming.UpdateParticipants();

    _notifier.Add(notifier);

    RaceRun rLast = null;
    foreach (var r in _race.GetRuns())
    {
      System.Diagnostics.Debug.Assert(rLast == null || rLast.Run < r.Run, "previous run number must be smaller than current run number");
      ObserveRaceRun(rLast, r);
      rLast = r;
    }
  }


  private void ObserveRaceRun(RaceRun previousRaceRun, RaceRun raceRun)
  {
    // Only the list of the current run is allowed to be sent.
    // => Startlist needs to be send in following cases:
    // a) Start list itself changes
    // b) The previous run completed so that the next run needs to be send
    ItemsChangedNotifier startListNotifier = new ItemsChangedNotifier(raceRun.GetStartListProvider().GetViewList());
    startListNotifier.CollectionChanged += (o, e) =>
    {
      updateStartList(previousRaceRun, raceRun);
    };
    updateStartList(previousRaceRun, raceRun); // Initial update
    _notifier.Add(startListNotifier);

    if (previousRaceRun != null)
    {
      IsCompleteObserver completeObserver = new IsCompleteObserver(previousRaceRun, raceRun, updateStartList);
      _notifier.Add(completeObserver);
    }

    // Results
    ItemsChangedNotifier resultsNotifier = new ItemsChangedNotifier(raceRun.GetResultList());
    resultsNotifier.ItemChanged += (o, e) =>
    {
      _liveTiming.UpdateResults(raceRun);
    };
    _liveTiming.UpdateResults(raceRun); // Initial update
    _notifier.Add(resultsNotifier);
  }


  private void updateStartList(RaceRun previousRaceRun, RaceRun raceRun)
  {
    if (previousRaceRun == null || previousRaceRun.IsComplete)
      _liveTiming.UpdateStartList(raceRun);
  }

}




public class LiveTimingRM : ILiveTiming
{
  private Race _race;
  private string _bewerbnr;
  private string _password;

  private bool _isOnline;
  private bool _started;
  LiveTimingDelegator _delegator;

  string _statusText;

  rmlt.LiveTiming _lv;
  rmlt.LiveTiming.rmltStruct _currentLvStruct;


  private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


  public LiveTimingRM()
  {
    _isOnline = false;
    _started = false;
  }


  public Race Race
  {
    set
    {
      if (LoggedOn || Started)
        throw new Exception("Race cannot be set if already connected");

      _race = value;
    }

    get
    {
      return _race;
    }
  }


  public void Login(string bewerbnr, string password)
  {
    _bewerbnr = bewerbnr;
    _password = password;

    _lv = new rmlt.LiveTiming();

    loginInternal();
  }

  public bool LoggedOn
  {
    get { return _isOnline; }
  }



  public void Start()
  {
    // Check if event was setup first
    if (string.IsNullOrEmpty(_currentLvStruct.VeranstNr))
      return;

    _started = true;

    Task.Run(() => {
      startLiveTiming();
      sendClassesAndGroups();
    });

    // Observes for changes and triggers UpdateMethods, also sends data initially
    _delegator = new LiveTimingDelegator(_race, this);

    var handler = StatusChanged;
    if (handler != null)
      handler.Invoke();
  }

  public void Stop()
  {
    if (!_started)
      return;

    _started = false;

    var handler = StatusChanged;
    if (handler != null)
      handler.Invoke();

    _delegator.Dispose();
  }


  public event OnStatusChanged StatusChanged;

  public bool Started
  {
    get { return _started; }
  }


  public List<string> GetEvents()
  {
    return _currentLvStruct.Veranstaltungen;
  }


  public void SetEvent(int no)
  {
    _currentLvStruct.VeranstNr = (no + 1).ToString();
  }



  public void UpdateParticipants()
  {
    sendParticipants();
  }

  public void UpdateStartList(RaceRun raceRun)
  {
    sendStartList(raceRun);
  }

  public void UpdateResults(RaceRun raceRun)
  {
    sendTiming(raceRun);
  }



  public void UpdateStatus(string statusText)
  {
    _statusText = statusText;
    updateStatus();
  }


  protected bool isOnline()
  {
    return _isOnline;
  }


  protected void loginInternal()
  {
    if (isOnline())
      return;

    Logger.Info("login");
    _currentLvStruct = _lv.LoginLiveTiming(_bewerbnr, _password);

    if (_currentLvStruct.Fehlermeldung != "ok")
    {
      throw new Exception("Login error: " + _currentLvStruct.Fehlermeldung);
    }

    _isOnline = true;

    startLiveTiming();
  }


  internal void startLiveTiming()
  {
    _currentLvStruct.Durchgaenge = string.Format("{0}", _race.GetMaxRun());

    // "add", "diff"
    _currentLvStruct.TypZeiten = "add";
    if (!string.IsNullOrEmpty(_race.RaceConfiguration.RaceResultView))
      if (_race.RaceConfiguration.RaceResultView.Contains("BestOfTwo"))
        _currentLvStruct.TypZeiten = "diff"; 

    // "Klasse", "Gruppe", "Kategorie"
    _currentLvStruct.Gruppierung = "Kategorie";
    if (!string.IsNullOrEmpty(_race.RaceConfiguration.DefaultGrouping))
      if (_race.RaceConfiguration.DefaultGrouping.Contains("Class") && _race.GetDataModel().GetParticipantClasses().Count > 0)
        _currentLvStruct.Gruppierung = "Klasse";
      else if (_race.RaceConfiguration.DefaultGrouping.Contains("Group") && _race.GetDataModel().GetParticipantGroups().Count > 0)
        _currentLvStruct.Gruppierung = "Gruppe";
      else if (_race.RaceConfiguration.DefaultGrouping.Contains("Sex"))
        _currentLvStruct.Gruppierung = "Kategorie";

    Logger.Info("startLiveTiming: {0}", _currentLvStruct);
    _lv.StartLiveTiming(ref _currentLvStruct);

    lastSendParticipantData = "";
    lastSendStartList = "";
    lastSendTimingData = "";
  }


  protected void updateStatus()
  {
    if (!isOnline() || _currentLvStruct.InfoText == _statusText)
      return;

    scheduleTransfer(new LTTransferStatus(_lv, _currentLvStruct, _statusText));
  }



  protected void sendClassesAndGroups()
  {
    //Typ|Id|Bezeichung|sortpos
    // \n
    //Typ Klasse / Gruppe / Kategorie
    //Id Id der Klasse / Gruppe / Kategorie
    //Bezeichnung Bezeichnung der Klasse / Gruppe / Kategorie
    //sortpos Reihenfolge innerhalb der Klasse / Gruppe / Kategorie

    string data = "";

    var classes = getClasses();
    var groups = getGroups();
    var cats= getCategories();

    if (!string.IsNullOrEmpty(classes))
      data += classes + "\n";
    if (!string.IsNullOrEmpty(groups))
      data += groups + "\n";
    if (!string.IsNullOrEmpty(cats))
      data += cats;

    Task task = Task.Run(() =>
    {
      Logger.Info("sendClassesAndGroups: " + data);
      _lv.SendKlassen(ref _currentLvStruct, data);
    });
  }


  internal string getClasses()
  {
    string result = "";

    var classes = _race.GetDataModel().GetParticipantClasses().ToList();
    classes.Sort();

    foreach (var c in classes)
    {
      string item;
      item = string.Format("Klasse|{0}|{1}|{2}", c.Id, c.Name, c.SortPos);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  internal string getGroups()
  {
    string result = "";

    var groups = _race.GetDataModel().GetParticipantGroups().ToList();
    groups.Sort();

    foreach (var c in groups)
    {
      string item;
      item = string.Format("Gruppe|{0}|{1}|{2}", c.Id, c.Name, c.SortPos);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  // Echten Daten übertragen
  internal string getCategories()
  {
    var cats = _race.GetDataModel().GetParticipantCategories().ToList();
    cats.Sort();

    string result = "";
    foreach (var c in cats)
    {
      string item;
      item = string.Format("Kategorie|{0}|{1}|{2}", c.Name, c.PrettyName, c.SortPos);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  protected string lastSendParticipantData = "";
  protected void sendParticipants()
  {
    // Id - Kategorie | Id - Gruppe | Id - Klasse | Id - Teilnehmer | Start - Nr | Code | Name | Jahrgang | Verband | Verein | Punkte

    // Id - Kategorie Id der Kategorie(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Gruppe Id der Gruppe(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Klasse Id der Klasse(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Teilnehmer Id des Teilnehmers(wird in Datei mit Zeiten verwendet)
    // Start - Nr Start - Nr des Teilnehmers
    // Code leer/ Code / DSV - Id des Teilnehmers
    // Name Name des Teilnehmers(NACHANME Vorname)
    // Jahrgang Jahrgang des Teilnehmers(4 - stellig) 
    // Verband leer/ Verband / Nation des Teilnehmers
    // Verein Verein des Teilnehmers
    // Punkte leer/ Punkte des Teilnehmers(mit Komma und 2 Nachkommastellen) 

    string data = getParticipantsData();
    if (data != lastSendParticipantData)
    {
      scheduleTransfer(new LTTransferParticpants(_lv, _currentLvStruct, data));
      lastSendParticipantData = data;
    }
  }


  internal string getParticipantsData()
  {
    string result = "";

    var participants = _race.GetParticipants();
    foreach(var participant in participants)
    {
      string item;
      item = getParticpantData(participant);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  internal string getParticpantData(RaceParticipant particpant)
  {
    // Id - Kategorie | Id - Gruppe | Id - Klasse | Id - Teilnehmer | Start - Nr | Code | Name | Jahrgang | Verband | Verein | Punkte

    // Id - Kategorie Id der Kategorie(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Gruppe Id der Gruppe(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Klasse Id der Klasse(muss in Datei mit Klasseneinteilungen vorhanden sein)
    // Id - Teilnehmer Id des Teilnehmers(wird in Datei mit Zeiten verwendet)
    // Start - Nr Start - Nr des Teilnehmers
    // Code leer/ Code / DSV - Id des Teilnehmers
    // Name Name des Teilnehmers(NACHANME Vorname)
    // Jahrgang Jahrgang des Teilnehmers(4 - stellig) 
    // Verband leer/ Verband / Nation des Teilnehmers
    // Verein Verein des Teilnehmers
    // Punkte leer/ Punkte des Teilnehmers(mit Komma und 2 Nachkommastellen) 
    string item;

    var customFormat = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
    customFormat.NumberFormat.NumberDecimalSeparator = ",";

    item = string.Format(
      customFormat,
      "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10:0.00}"
      , particpant.Sex?.Name
      , particpant.Class?.Group?.Id
      , particpant.Class?.Id
      , particpant.Participant.Id
      , particpant.StartNumber
      , particpant.Participant.CodeOrSvId // TODO: set to empty if not used
      , particpant.Participant.Fullname
      , particpant.Year
      , particpant.Nation   // TODO: set to empty if not used
      , particpant.Club
      , particpant.Points
      );

    return item;
  }


  protected string lastSendStartList = "";
  protected void sendStartList(RaceRun raceRun)
  {

    //iii
    //iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)

    string data = getStartListData(raceRun);
    string dg = string.Format("{0}", raceRun.Run);

    if (lastSendStartList != data + dg)
    {
      scheduleTransfer(new LTTransferStartList(_lv, _currentLvStruct, dg, data));
      lastSendStartList = data + dg;
    }
  }


  internal string getStartListData(RaceRun raceRun)
  {
    string result = "";

    StartListViewProvider slp = raceRun.GetStartListProvider();
    var startList = slp.GetViewList();

    foreach (var sle in startList)
    {
      string item;

      item = string.Format("{0,3}", sle.Participant.Id);
      
      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  string lastSendTimingData = "";
  protected void sendTiming(RaceRun raceRun)
  {
    // iiiehhmmss,zhdistext
    // iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)
    // e ErgCode: 0 = Läufer im Ziel Laufzeit = hhmmss,zh
    //            1 = Nicht am Start Laufzeit = 999999,99
    //            2 = Nicht im Ziel Laufzeit = 999999,99
    //            3 = Disqualifiziert Laufzeit = 999999,99
    //            4 = Nicht qualifiziert Laufzeit = 999999,99
    //            9 = Läufer auf der Strecke Laufzeit = 000000,01
    // hhmmss,zh Laufzeit
    // distext   Disqualifikationstext

    string data = getTimingData(raceRun);
    string dg = string.Format("{0}", raceRun.Run);

    if (lastSendTimingData != data + dg)
    {
      scheduleTransfer(new LTTransferTiming(_lv, _currentLvStruct, dg, data));
      lastSendTimingData = data + dg;
    }
  }


  internal string getTimingData(RaceRun raceRun)
  {
    // iiiehhmmss,zhdistext
    // iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)
    // e ErgCode: 0 = Läufer im Ziel Laufzeit = hhmmss,zh
    //            1 = Nicht am Start Laufzeit = 999999,99
    //            2 = Nicht im Ziel Laufzeit = 999999,99
    //            3 = Disqualifiziert Laufzeit = 999999,99
    //            4 = Nicht qualifiziert Laufzeit = 999999,99
    //            9 = Läufer auf der Strecke Laufzeit = 000000,01 oder Startzeit
    // hhmmss,zh Laufzeit
    // distext   Disqualifikationstext

    string result = "";

    List<RunResult> results = raceRun.GetResultList().OrderBy(o => o.StartNumber).ToList();
    foreach (var r in results)
    {
      string item, time, distext;
      int eCode;

      distext = "";
      if (r.ResultCode == RunResult.EResultCode.Normal)
      {
        if (r.Runtime != null)
        {
          time = r.Runtime?.ToString(@"hhmmss\,ff", System.Globalization.CultureInfo.InvariantCulture);
          eCode = 0;
        }
        else if (r.GetStartTime() != null)
        {
          if (r.StartTime != null)
            time = r.StartTime?.ToString(@"hhmmss\,ff", System.Globalization.CultureInfo.InvariantCulture);
          else
            time = "000000,01";
          eCode = 9;
        }
        else
          // No useful data => skip
          continue;
      }
      else
      {
        time = "999999,99";
        switch (r.ResultCode)
        {
          case RunResult.EResultCode.NaS: eCode = 1; break;
          case RunResult.EResultCode.NiZ: eCode = 2; break;
          case RunResult.EResultCode.DIS: eCode = 3; distext = r.DisqualText; break;
          case RunResult.EResultCode.NQ:  eCode = 4; break;
          default:
            // No useful data => skip
            continue;
        }
        
      }

      item = string.Format("{0,3}{1,1}{2}{3}", r.Participant.Id, eCode, time, distext);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    // Add current day time
    var dayTime = raceRun.GetRace().GetDataModel().GetCurrentDayTime();
    var curTimeString = string.Format("{0,3}{1,1}{2}", 999, ' ', dayTime.ToString(@"hhmmss\,ff", System.Globalization.CultureInfo.InvariantCulture));
    if (!string.IsNullOrEmpty(result))
      result += "\n";
    result += curTimeString;

    return result;
  }


  List<LTTransfer> _transfers = new List<LTTransfer>();
  object _transferLock = new object();
  bool _transferInProgress = false;
    
  private void scheduleTransfer(LTTransfer transfer)
  {
    lock (_transferLock)
    {
      // Remove all outdated transfers
      _transfers.RemoveAll(x => x.IsEqual(transfer));
      _transfers.Add(transfer);
    }

    if (!_transferInProgress)
    {
      _transferInProgress = true;
      processNextTransfer();
    }
  }


  private void processNextTransfer()
  {
    LTTransfer nextItem = null;
    lock (_transferLock)
    {
      if (_transfers.Count() > 0)
      {
        nextItem = _transfers[0];
        _transfers.RemoveAt(0);
      }
    }

    if (nextItem != null)
    {
      // Trigger execution of transfers
      Task.Run( () => 
        {
          Logger.Debug("process transfer: " + nextItem.ToString());
          nextItem.performTask();
        })
        .ContinueWith(delegate { processNextTransfer(); });
    }
    else
      _transferInProgress = false;
  }
}


public abstract class LTTransfer
{
  protected rmlt.LiveTiming _lv;
  protected rmlt.LiveTiming.rmltStruct _currentLvStruct;

  protected string _type;

  protected LTTransfer(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct currentLvStruct, string type)
  {
    _lv = lv;
    _currentLvStruct = currentLvStruct;
    _type = type;
  }

  public override string ToString()
  {
    return "LTTransfer(" + _type + ")";
  }


  public abstract bool IsEqual(LTTransfer other);

  public bool IsSameType(LTTransfer other)
  {
    return _type == other._type;
  }

  public abstract void performTask();

}


public class LTTransferStatus : LTTransfer
{
  string _data;

  public LTTransferStatus(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct lvStruct, string status)
    : base(lv, lvStruct, "status")
  {
    _data = status;
  }

  public override string ToString()
  {
    return "LTTransfer(" + _type + "," + _data + ")";
  }
   
  public override bool IsEqual(LTTransfer other)
  {
    if (IsSameType(other) && other is LTTransferStatus otherStatus)
    {
      return _data == otherStatus._data;
    }

    return false;
  }

  public override void performTask()
  {
    _currentLvStruct.InfoText = _data;
    _lv.SendInfo(ref _currentLvStruct);
  }

}

public class LTTransferParticpants : LTTransfer
{
  string _data;

  public LTTransferParticpants(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct lvStruct, string data)
    : base(lv, lvStruct, "particpants")
  {
    _data = data;
  }

  public override string ToString()
  {
    return "LTTransfer(" + _type + "," + _data + ")";
  }

  public override bool IsEqual(LTTransfer other)
  {
    if (IsSameType(other) && other is LTTransferParticpants otherStatus)
    {
      return _data == otherStatus._data;
    }

    return false;
  }

  public override void performTask()
  {
    _lv.SendTeilnehmer(ref _currentLvStruct, _data);
  }

}


public class LTTransferStartList : LTTransfer
{
  string _dg;
  string _data;

  public LTTransferStartList(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct lvStruct, string dg, string data)
    : base(lv, lvStruct, "startlist")
  {
    _dg = dg;
    _data = data;
  }

  public override string ToString()
  {
    return "LTTransfer(" + _type + "," + _dg + "," + _data + ")";
  }

  public override bool IsEqual(LTTransfer other)
  {
    if (IsSameType(other) && other is LTTransferStartList otherStatus)
    {
      return _dg == otherStatus._dg && _data == otherStatus._data;
    }

    return false;
  }

  public override void performTask()
  {
    _lv.SendStartliste(ref _currentLvStruct, _dg, _data);
  }

}


public class LTTransferTiming : LTTransfer
{
  string _dg;
  string _data;

  public LTTransferTiming(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct lvStruct, string dg, string data)
    : base(lv, lvStruct, "timing")
  {
    _dg = dg;
    _data = data;
  }

  public override string ToString()
  {
    return "LTTransfer(" + _type + "," + _dg + "," + _data + ")";
  }

  public override bool IsEqual(LTTransfer other)
  {
    if (IsSameType(other) && other is LTTransferTiming otherStatus)
    {
      return _dg == otherStatus._dg && _data == otherStatus._data;
    }

    return false;
  }

  public override void performTask()
  {
    _lv.SendZeiten(ref _currentLvStruct, _dg, _data);
  }

}


