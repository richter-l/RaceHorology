﻿using DSVAlpin2Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


public interface ILiveTiming
{
  void UpdateParticipants();

  void UpdateStartList(RaceRun raceRun);

  void UpdateResults(RaceRun raceRun);

}

public class LiveTimingDelegator
{
  Race _race;
  ILiveTiming _liveTiming;

  List<ItemsChangedNotifier> _notifier;

  public LiveTimingDelegator(Race race, ILiveTiming liveTiming)
  {
    _race = race;
    _liveTiming = liveTiming;

    _notifier = new List<ItemsChangedNotifier>();
    
    ObserveRace();
  }


  private void ObserveRace()
  {
    ItemsChangedNotifier notifier = new ItemsChangedNotifier(_race.GetParticipants());
    notifier.CollectionChanged += (o, e) =>
    {
      _liveTiming.UpdateParticipants();
    };
    _liveTiming.UpdateParticipants();

    _notifier.Add(notifier);

    foreach (var r in _race.GetRuns())
      ObserveRaceRun(r);
  }


  private void ObserveRaceRun(RaceRun raceRun)
  {
    ItemsChangedNotifier startListNotifier = new ItemsChangedNotifier(raceRun.GetStartListProvider().GetViewList());
    startListNotifier.ItemChanged += (o, e) =>
    {
      _liveTiming.UpdateStartList(raceRun);
    };
    _liveTiming.UpdateStartList(raceRun);
    _notifier.Add(startListNotifier);

    ItemsChangedNotifier resultsNotifier = new ItemsChangedNotifier(raceRun.GetResultList());
    resultsNotifier.ItemChanged += (o, e) =>
    {
      _liveTiming.UpdateResults(raceRun);
    };
    _liveTiming.UpdateResults(raceRun);
    _notifier.Add(resultsNotifier);
  }
}




public class LiveTimingRM : ILiveTiming
{
  private Race _race;
  private string _bewerbnr;
  private string _login;
  private string _password;

  private bool _isOnline;
  private bool _started;
  LiveTimingDelegator _delegator;

  string _statusText;

  rmlt.LiveTiming _lv;
  rmlt.LiveTiming.rmltStruct _currentLvStruct;

  public LiveTimingRM(Race race, string bewerbnr, string login, string password)
  {
    _race = race;
    _bewerbnr = bewerbnr;
    _login = login;
    _password = password;

    _isOnline = false;
    _started = false;
  }


  public void Login()
  {
    _lv = new rmlt.LiveTiming();

    login();
  }



  public void Start(int noEvent)
  {
    if (_started)
      return;

    _started = true;

    SetEvent(noEvent);

    Task.Run(() => {
      startLiveTiming();
      sendClassesAndGroups();
    });

    // Observes for changes and triggers UpdateMethods, also sends data initially
    _delegator = new LiveTimingDelegator(_race, this);
  }

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


  protected void login()
  {
    if (isOnline())
      return;

    string licensePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    licensePath = Path.Combine(licensePath, "3rdparty");

    _currentLvStruct = _lv.LoginLiveTiming(_bewerbnr, _login, _password, licensePath);

    if (_currentLvStruct.Fehlermeldung != "ok")
      throw new Exception("Login error: " + _currentLvStruct.Fehlermeldung);

    _isOnline = true;

    _lv.StartLiveTiming(ref _currentLvStruct);
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
    _currentLvStruct.Gruppierung = "Klasse";
    if (!string.IsNullOrEmpty(_race.RaceConfiguration.DefaultGrouping))
      if (_race.RaceConfiguration.DefaultGrouping.Contains("Class"))
        _currentLvStruct.Gruppierung = "Klasse";
      else if (_race.RaceConfiguration.DefaultGrouping.Contains("Group"))
        _currentLvStruct.Gruppierung = "Gruppe";
      else if (_race.RaceConfiguration.DefaultGrouping.Contains("Sex"))
        _currentLvStruct.Gruppierung = "Kategorie";

    _lv.StartLiveTiming(ref _currentLvStruct);
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

    data += getClasses() + "\n" + getGroups() + "\n" + getCategories();

    Task task = Task.Run(() =>
    {
      _lv.SendKlassen(ref _currentLvStruct, data);
    });
  }


  internal string getClasses()
  {
    string result = "";
    
    foreach (var c in _race.GetDataModel().GetParticipantClasses())
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

    foreach (var c in _race.GetDataModel().GetParticipantGroups())
    {
      string item;
      item = string.Format("Gruppe|{0}|{1}|{2}", c.Id, c.Name, c.SortPos);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  internal string getCategories()
  {
    return "Kategorie|M|M|1\nKategorie|W|W|2";
  }


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

    string data = "";

    data = getParticipantsData();

    scheduleTransfer(new LTTransferParticpants(_lv, _currentLvStruct, data));
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

    item = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}"
      , particpant.Sex
      , particpant.Class.Group.Id
      , particpant.Class.Id
      , particpant.Participant.Id
      , particpant.StartNumber
      , particpant.Participant.CodeOrSvId // TODO: set to empty if not used
      , particpant.Participant.Fullname
      , particpant.Year
      , particpant.Nation   // TODO: set to empty if not used
      , particpant.Club
      , particpant.Points); // TODO: set to empty if not used

    return item;
  }


  protected void sendStartList(RaceRun raceRun)
  {
    //iii
    //iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)

    string data = "";

    data = getStartListData(raceRun);

    string dg = string.Format("{0}", raceRun.Run);

    scheduleTransfer(new LTTransferStartList(_lv, _currentLvStruct, dg, data));
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


  protected void sendTiming(RaceRun raceRun)
  {
    // iiiehhmmss,zh
    // iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)
    // e ErgCode: 0 = Läufer im Ziel Laufzeit = hhmmss,zh
    //            1 = Nicht am Start Laufzeit = 999999,99
    //            2 = Nicht im Ziel Laufzeit = 999999,99
    //            3 = Disqualifiziert Laufzeit = 999999,99
    //            4 = Nicht qualifiziert Laufzeit = 999999,99
    //            9 = Läufer auf der Strecke Laufzeit = 000000,01
    // hhmmss,zh Laufzeit

    string data = "";

    data = getTimingData(raceRun);

    string dg = string.Format("{0}", raceRun.Run);


    scheduleTransfer(new LTTransferTiming(_lv, _currentLvStruct, dg, data));
  }


  internal string getTimingData(RaceRun raceRun)
  {
    // iiiehhmmss,zh
    // iii Id des Teilnehmers(muss in Datei mit Teilnehmerdaten vorhanden sein)
    // e ErgCode: 0 = Läufer im Ziel Laufzeit = hhmmss,zh
    //            1 = Nicht am Start Laufzeit = 999999,99
    //            2 = Nicht im Ziel Laufzeit = 999999,99
    //            3 = Disqualifiziert Laufzeit = 999999,99
    //            4 = Nicht qualifiziert Laufzeit = 999999,99
    //            9 = Läufer auf der Strecke Laufzeit = 000000,01
    // hhmmss,zh Laufzeit

    string result = "";

    List<RunResult> results = raceRun.GetResultList().OrderBy(o => o.StartNumber).ToList();
    foreach (var r in results)
    {
      string item, time;
      int eCode;

      if (r.ResultCode == RunResult.EResultCode.Normal)
      {
        if (r.Runtime != null)
        {
          time = r.Runtime?.ToString(@"hhmmss\,ff");
          eCode = 0;
        }
        else if (r.GetStartTime() != null)
        {
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
          case RunResult.EResultCode.DIS: eCode = 3; break;
          case RunResult.EResultCode.NQ:  eCode = 4; break;
          default:
            // No useful data => skip
            continue;
        }
      }

      item = string.Format("{0,3}{1,1}{2}", r.Participant.Id, eCode, time);

      if (!string.IsNullOrEmpty(result))
        result += "\n";

      result += item;
    }

    return result;
  }


  List<LTTransfer> _transfers = new List<LTTransfer>();
  object _transferLock = new object();
    
  private void scheduleTransfer(LTTransfer transfer)
  {
    // Remove all outdated transfers
    lock (_transferLock)
    {
      _transfers.RemoveAll(x => x.IsSameType(transfer));
      _transfers.Add(transfer);
    }
    processNextTransfer(); 
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
      Task.Run(nextItem.performTask).ContinueWith(delegate { processNextTransfer(); });
    }
  }
}


public abstract class LTTransfer
{
  protected rmlt.LiveTiming _lv;
  protected rmlt.LiveTiming.rmltStruct _currentLvStruct;

  string _type;

  protected LTTransfer(rmlt.LiveTiming lv, rmlt.LiveTiming.rmltStruct currentLvStruct, string type)
  {
    _lv = lv;
    _currentLvStruct = currentLvStruct;
    _type = type;
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
    _lv.StartLiveTiming(ref _currentLvStruct);
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


