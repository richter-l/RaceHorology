﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSVAlpin2Lib
{

  /// <summary>
  /// Implements the data base access to and from the "old" DSVAlpin Access Data Base
  /// </summary>
  /// <remarks>not yet fully implemented</remarks>
  public class Database
    : IAppDataModelDataBase
  {
    private System.Data.OleDb.OleDbConnection _conn;

    private Dictionary<uint, Participant> _id2Participant;

    public void Connect(string filename)
    {
      _conn = new OleDbConnection
      {
        ConnectionString = @"Provider=Microsoft.Jet.OLEDB.4.0; Data source= " + filename
      };

      try
      {
        _conn.Open();
      }
      catch (Exception ex)
      {
        Console.WriteLine("Failed to connect to data source", ex.Message);
        _conn = null;
        throw;
      }

      // Setup internal daat structures
      _id2Participant = new Dictionary<uint, Participant>();
    }

    public void Close()
    {
      // Cleanup internal data structures
      _id2Participant = null;

      _conn.Close();
      _conn = null;
    }

    public ItemsChangeObservableCollection<Participant> GetParticipants()
    {
      ItemsChangeObservableCollection<Participant> participants = new ItemsChangeObservableCollection<Participant>();

      string sql = @"SELECT * FROM tblTeilnehmer";

      OleDbCommand command = new OleDbCommand(sql, _conn);
      // Execute command  
      using (OleDbDataReader reader = command.ExecuteReader())
      {
        while (reader.Read())
        {
          participants.Add(CreateParticipantFromDB(reader));
        }
      }

      return participants;
    }


    public RaceRun GetRaceRun(uint run)
    {
      RaceRun raceRun = new RaceRun(run);

      string sql = @"SELECT tblZeit.*, tblZeit.durchgang, tblZeit.disziplin, tblTeilnehmer.startnrsg, tblTeilnehmer.startnrgs, tblTeilnehmer.startnrsl, tblTeilnehmer.startnrks, tblTeilnehmer.startnrps "+
                   @"FROM tblTeilnehmer INNER JOIN tblZeit ON tblTeilnehmer.id = tblZeit.teilnehmer "+
                   @"WHERE(((tblZeit.durchgang) = @durchgang) AND((tblZeit.disziplin) = 2))";

      OleDbCommand command = new OleDbCommand(sql, _conn);
      command.Parameters.Add(new OleDbParameter("@durchgang", run));

      // Execute command  
      using (OleDbDataReader reader = command.ExecuteReader())
      {
        while (reader.Read())
        {
          // Get Participant
          uint id = (uint)(int)reader.GetValue(reader.GetOrdinal("teilnehmer"));
          Participant p = _id2Participant[id];

          // Build Result
          TimeSpan? runTime = null, startTime = null, finishTime = null;
          if (!reader.IsDBNull(reader.GetOrdinal("netto")))
            runTime = CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("netto")));
          if (!reader.IsDBNull(reader.GetOrdinal("start")))
            startTime = CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("start")));
          if (!reader.IsDBNull(reader.GetOrdinal("ziel")))
            finishTime = CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("ziel")));

          RunResult r = new RunResult
          {
            _participant = p
          };
          if (startTime!=null)
            r.SetStartTime((TimeSpan)startTime);
          if (finishTime != null)
            r.SetFinishTime((TimeSpan)finishTime);
          if (runTime != null)
            r.SetRunTime((TimeSpan)runTime);

          raceRun.InsertResult(r);
        }
      }

      return raceRun;
    }



    private Participant CreateParticipantFromDB(OleDbDataReader reader)
    {
      uint id = (uint)(int)reader.GetValue(reader.GetOrdinal("id"));

      if (_id2Participant.ContainsKey(id))
        return _id2Participant[id];
      else
      {
        Participant p = new Participant
        {
          Name = reader["nachname"].ToString(),
          Firstname = reader["vorname"].ToString(),
          Sex = reader["sex"].ToString(),
          Club = reader["verein"].ToString(),
          Nation = reader["nation"].ToString(),
          Class = GetClass(GetValueUInt(reader, "klasse")),
          Year = reader.GetInt16(reader.GetOrdinal("jahrgang")),
          StartNumber = GetStartNumber(reader)
        };
        _id2Participant.Add(id, p);

        return p;
      }
    }


    static private uint GetValueUInt(OleDbDataReader reader, string field)
    {
      if (!reader.IsDBNull(reader.GetOrdinal(field)))
      {
        var v = reader.GetValue(reader.GetOrdinal(field));
        return Convert.ToUInt32(v);
      }

      return 0;
    }


    /// <summary>
    /// Determines and returns the startnumber
    /// </summary>
    /// <returns>
    /// 0 if no startnumber is assigned
    /// </returns>
    static private uint GetStartNumber(OleDbDataReader reader)
    {
      uint sn = 0;
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrdh");
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrsg");
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrgs");
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrsl");
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrks");
      if (sn == 0)
        sn = GetValueUInt(reader, "startnrps");

      return sn;
    }


    private Dictionary<uint, string> _id2Class;
    private string GetClass(uint idClass)
    {
      if (_id2Class == null)
      {
        _id2Class = new Dictionary<uint, string>();

        // Get Classes from DB
        string sql = @"SELECT * FROM tblKlasse";

        OleDbCommand command = new OleDbCommand(sql, _conn);
        // Execute command  
        using (OleDbDataReader reader = command.ExecuteReader())
        {
          while (reader.Read())
          {
            uint id = GetValueUInt(reader, "id");
            string classname = reader["klname"].ToString();
            _id2Class.Add(id, classname);
          }
        }
      }

      return _id2Class[idClass];
  }


    #region TimeSpan and Fraction
    const Int64 nanosecondsPerDay = 24L * 60 * 60 * 1000 * 1000 * 10;

    static public TimeSpan CreateTimeSpan(double fractionPerDay)
    {
      Int64 ticks = (Int64)(nanosecondsPerDay * fractionPerDay + .5);
      TimeSpan ts = new TimeSpan(ticks); // unit: 1 tick = 100 nanoseconds
      return ts;
    }

    static public double FractionForTimeSpan(TimeSpan ts)
    {
      return (double)ts.Ticks / nanosecondsPerDay;
    }
    #endregion

  }
}
