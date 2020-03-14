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
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RaceHorologyLib;
using System.IO;
using System.Data.OleDb;
using System.Linq;

namespace RaceHorologyLibTest
{
  /// <summary>
  /// Summary description for DSVAlpinDatabaseTests
  /// </summary>
  [TestClass]
  public class DSVAlpinDatabaseTests
  {
    public DSVAlpinDatabaseTests()
    {
    }

    private TestContext testContextInstance;

    /// <summary>
    ///Gets or sets the test context which provides
    ///information about and functionality for the current test run.
    ///</summary>
    public TestContext TestContext
    {
      get
      {
        return testContextInstance;
      }
      set
      {
        testContextInstance = value;
      }
    }

    #region Additional test attributes
    //
    // You can use the following additional attributes as you write your tests:
    //
    // Use ClassInitialize to run code before running the first test in the class
    // [ClassInitialize()]
    // public static void MyClassInitialize(TestContext testContext) { }
    //
    // Use ClassCleanup to run code after all tests in a class have run
    // [ClassCleanup()]
    // public static void MyClassCleanup() { }
    //
    // Use TestInitialize to run code before running each test 
    // [TestInitialize()]
    // public void MyTestInitialize() { }
    //
    // Use TestCleanup to run code after each test has run
    // [TestCleanup()]
    // public void MyTestCleanup() { }
    //
    #endregion


    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void DatabaseOpenClose()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");

      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      var participants = db.GetParticipants();

      Assert.IsTrue(participants.Count() == 5);
      Assert.IsTrue(participants.Where(x => x.Name == "Nachname 3").Count() == 1);

      db.Close();
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants_MultipleRaces.mdb")]
    public void DatabaseRaces()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants_MultipleRaces.mdb");

      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      var races = db.GetRaces();

      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.DownHill).First();
        Assert.AreEqual(2U, race.Runs);
        Assert.AreEqual(null, race.RaceNumber);
        Assert.AreEqual("Abfahrt - Bezeichnung 1\r\nAbfahrt - Bezeichnung 2", race.Description);
        Assert.AreEqual(new DateTime(2019, 1, 19), race.DateStart);
        Assert.AreEqual(new DateTime(2019, 1, 19), race.DateResult);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.SuperG).First();
        Assert.AreEqual(1U, race.Runs);
        Assert.AreEqual("20190120_B", race.RaceNumber);
        Assert.AreEqual("Super G Bezeichnung 1\r\nSuper G Bezeichnung 2", race.Description);
        Assert.AreEqual(new DateTime(2019, 1, 18), race.DateStart);
        Assert.AreEqual(new DateTime(), race.DateResult);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.GiantSlalom).First();
        Assert.AreEqual(2U, race.Runs);
        Assert.AreEqual("20190120_C", race.RaceNumber);
        Assert.AreEqual("Riesenslalom Bezeichnung 1\r\nRiesenslalom Bezeichnung 2", race.Description);
        Assert.AreEqual(new DateTime(), race.DateStart);
        Assert.AreEqual(new DateTime(2019, 1, 20), race.DateResult);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.Slalom).First();
        Assert.AreEqual(1U, race.Runs);
        Assert.AreEqual("20190120_D", race.RaceNumber);
        Assert.AreEqual(null, race.Description);
        Assert.AreEqual(new DateTime(2019, 2, 21), race.DateStart);
        Assert.AreEqual(new DateTime(2019, 1, 21), race.DateResult);
      }

      //
      Assert.AreEqual(0, races.Where(r => r.RaceType == Race.ERaceType.ParallelSlalom).Count());
      Assert.AreEqual(0, races.Where(r => r.RaceType == Race.ERaceType.KOSlalom).Count());
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants_MultipleRaces.mdb")]
    public void DatabaseRaceParticipants()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants_MultipleRaces.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      var races = db.GetRaces();
      AppDataModel model = new AppDataModel(db);

      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.DownHill).First();
        var raceParticipants = db.GetRaceParticipants(new Race(db, model, race));
        Assert.AreEqual(6, raceParticipants.Count());
        Assert.AreEqual(1, raceParticipants.Where(p => p.Participant.Name == "Nachname 6").Count());
        Assert.AreEqual(0, raceParticipants.Where(p => p.Participant.Name == "Nachname 10").Count());

        Assert.AreEqual(100.0, raceParticipants.Where(p => p.Participant.Name == "Nachname 6").First().Points);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.SuperG).First();
        var raceParticipants = db.GetRaceParticipants(new Race(db, model, race));
        Assert.AreEqual(6, raceParticipants.Count());
        Assert.AreEqual(1, raceParticipants.Where(p => p.Participant.Name == "Nachname 7").Count());
        Assert.AreEqual(0, raceParticipants.Where(p => p.Participant.Name == "Nachname 10").Count());

        Assert.AreEqual(200.1, raceParticipants.Where(p => p.Participant.Name == "Nachname 7").First().Points);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.GiantSlalom).First();
        var raceParticipants = db.GetRaceParticipants(new Race(db, model, race));
        Assert.AreEqual(6, raceParticipants.Count());
        Assert.AreEqual(1, raceParticipants.Where(p => p.Participant.Name == "Nachname 8").Count());
        Assert.AreEqual(0, raceParticipants.Where(p => p.Participant.Name == "Nachname 10").Count());

        Assert.AreEqual(9999.98, raceParticipants.Where(p => p.Participant.Name == "Nachname 8").First().Points);
      }
      {
        var race = races.Where(r => r.RaceType == Race.ERaceType.Slalom).First();
        var raceParticipants = db.GetRaceParticipants(new Race(db, model, race));
        Assert.AreEqual(6, raceParticipants.Count());
        Assert.AreEqual(1, raceParticipants.Where(p => p.Participant.Name == "Nachname 9").Count());
        Assert.AreEqual(0, raceParticipants.Where(p => p.Participant.Name == "Nachname 10").Count());

        Assert.AreEqual(0.0, raceParticipants.Where(p => p.Participant.Name == "Nachname 9").First().Points);
      }
    }


    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void DatabaseRaceRuns()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      db.GetParticipants();

      AppDataModel model = new AppDataModel(db);

      Race.RaceProperties rprops = new Race.RaceProperties();
      rprops.RaceType = Race.ERaceType.GiantSlalom;
      rprops.Runs = 2;
      Race race = new Race(db, model, rprops);

      var rr1 = db.GetRaceRun(race, 1);
      var rr2 = db.GetRaceRun(race, 2);

      Assert.IsTrue(rr1.Count() == 4);
      Assert.IsTrue(rr2.Count() == 4);

      Assert.IsTrue(rr1.Where(x => x.GetFinishTime() == null && x.GetStartTime() != null).First().Participant.Participant.Name == "Nachname 3");

      Assert.IsTrue(rr2.Where(x => x.GetFinishTime() == null && x.GetStartTime() != null).First().Participant.Participant.Name == "Nachname 2");

      Assert.IsTrue(rr2.Where(x => x.Participant.Participant.Name == "Nachname 5").Count() == 0);

      db.Close();
    }


    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void InitializeApplicationModel()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      AppDataModel model = new AppDataModel(db);
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_Empty.mdb")]
    public void CreateAndUpdateParticipants()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_Empty.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      var participants = db.GetParticipants();

      void DBCacheWorkaround()
      {
        db.Close(); // WORKAROUND: OleDB caches the update, so the Check would not see the changes
        db.Connect(dbFilename);
        participants = db.GetParticipants();
      }

      Participant pNew1 = new Participant
      {
        Name = "Nachname 6",
        Firstname = "Vorname 6",
        Sex = "M",
        Club = "Verein 6",
        Nation = "GER",
        SvId = "123",
        Code = "321",
        Class = db.GetParticipantClasses()[0],
        Year = 2009
      };
      db.CreateOrUpdateParticipant(pNew1);
      DBCacheWorkaround();
      Assert.IsTrue(CheckParticipant(dbFilename, pNew1, 1));
      

      Participant pNew2 = new Participant
      {
        Name = "Nachname 7",
        Firstname = "Vorname 7",
        Sex = "M",
        Club = "Verein 7",
        Nation = "GER",
        Class = db.GetParticipantClasses()[0],
        Year = 2010
      };
      db.CreateOrUpdateParticipant(pNew2);
      DBCacheWorkaround();
      Assert.IsTrue(CheckParticipant(dbFilename, pNew2, 2));


      // Create with non-mandatory properties
      Participant pNew3 = new Participant
      {
        Name = "Nachname 8",
        Firstname = "Vorname 8",
        Sex = "",
        Club = "",
        Nation = "",
        Class = db.GetParticipantClasses()[0],
        Year = 2010
      };
      db.CreateOrUpdateParticipant(pNew3);
      DBCacheWorkaround();
      Assert.IsTrue(CheckParticipant(dbFilename, pNew3, 3));
      

      // Update a Participant
      pNew1 = participants.Where(x => x.Name == "Nachname 6").FirstOrDefault();
      pNew1.Name = "Nachname 6.1";
      pNew1.Firstname = "Vorname 6.1";
      pNew1.Sex = "W";
      pNew1.Club = "Verein 6.1";
      pNew1.Nation = "GDR";
      pNew1.Class = db.GetParticipantClasses()[0];
      pNew1.Year = 2008;
      db.CreateOrUpdateParticipant(pNew1);
      DBCacheWorkaround();
      Assert.IsTrue(CheckParticipant(dbFilename, pNew1, 1));

      // Update with non-mandatory properties
      pNew1 = participants.Where(x => x.Name == "Nachname 6.1").FirstOrDefault();
      pNew1.Name = "Nachname 6.2";
      pNew1.Firstname = "Vorname 6.2";
      pNew1.Sex = "";
      pNew1.Club = "";
      pNew1.Nation = "";
      pNew1.Class = db.GetParticipantClasses()[0];
      pNew1.Year = 2008;
      db.CreateOrUpdateParticipant(pNew1);
      DBCacheWorkaround();
      Assert.IsTrue(CheckParticipant(dbFilename, pNew1, 1));

    }

    private bool CheckParticipant(string dbFilename, Participant participant, int id)
    {
      bool bRes = true;

      OleDbConnection conn = new OleDbConnection { ConnectionString = @"Provider=Microsoft.Jet.OLEDB.4.0; Data source= " + dbFilename };
      conn.Open();

      string sql = @"SELECT * FROM tblTeilnehmer WHERE id = @id";
      OleDbCommand command = new OleDbCommand(sql, conn);
      command.Parameters.Add(new OleDbParameter("@id", id));

      bool checkAgainstDB(string value, object vDB)
      {
        string sDB = vDB.ToString();
        if (string.IsNullOrEmpty(value) && (vDB == DBNull.Value))
          return true;

        return value == sDB;
      }

      // Execute command  
      using (OleDbDataReader reader = command.ExecuteReader())
      {
        if (reader.Read())
        {
          bRes &= participant.Name == reader["nachname"].ToString();
          bRes &= participant.Firstname == reader["vorname"].ToString();
          bRes &= participant.Sex == reader["sex"].ToString();
          bRes &= participant.Club == reader["verein"].ToString();
          bRes &= participant.Nation == reader["nation"].ToString();
          bRes &= checkAgainstDB(participant.SvId, reader["svid"]);
          bRes &= checkAgainstDB(participant.Code, reader["code"]);
          //bRes &= participant.Class == GetClass(GetValueUInt(reader, "klasse"));
          bRes &= participant.Year == reader.GetInt16(reader.GetOrdinal("jahrgang"));
          //bRes &= participant.StartNumber == GetStartNumber(reader);
        }
        else
          bRes = false;
      }

      conn.Close();

      return bRes;
    }


    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void CreateAndUpdateRunResults()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      AppDataModel dataModel = new AppDataModel(db);
      Race race = dataModel.GetCurrentRace();
      RaceRun rr1 = race.GetRun(0);
      RaceRun rr2 = race.GetRun(1);

      void DBCacheWorkaround()
      {
        db.Close(); // WORKAROUND: OleDB caches the update, so the Check would not see the changes
        db.Connect(dbFilename);
        dataModel = new AppDataModel(db);
        race = dataModel.GetCurrentRace();
        rr1 = race.GetRun(0);
        rr2 = race.GetRun(1);
      }


      RaceParticipant participant1 = race.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      RunResult rr1r1 = new RunResult(participant1);

      rr1r1.SetStartTime(new TimeSpan(0, 12, 0, 0, 0)); //int days, int hours, int minutes, int seconds, int milliseconds
      db.CreateOrUpdateRunResult(race, rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = race.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      rr1r1.SetStartTime(rr1r1.GetStartTime()); //int days, int hours, int minutes, int seconds, int milliseconds
      rr1r1.SetFinishTime(new TimeSpan(0, 12, 1, 0, 0)); //int days, int hours, int minutes, int seconds, int milliseconds
      db.CreateOrUpdateRunResult(race, rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = race.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      rr1r1.SetStartTime(null); //int days, int hours, int minutes, int seconds, int milliseconds
      rr1r1.SetFinishTime(null); //int days, int hours, int minutes, int seconds, int milliseconds
      rr1r1.SetRunTime(new TimeSpan(0, 0, 1, 1, 110)); //int days, int hours, int minutes, int seconds, int milliseconds
      db.CreateOrUpdateRunResult(race, rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = race.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));



      rr1r1.ResultCode = RunResult.EResultCode.DIS;
      rr1r1.DisqualText = "TF Tor 9";
      db.CreateOrUpdateRunResult(race, rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = race.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      RaceParticipant participant5 = race.GetParticipants().Where(x => x.Name == "Nachname 5").FirstOrDefault();
      RunResult rr5r1 = new RunResult(participant5);
      rr5r1.SetStartTime(new TimeSpan(0, 12, 1, 1, 1)); //int days, int hours, int minutes, int seconds, int milliseconds
      rr5r1.ResultCode = RunResult.EResultCode.NiZ;
      db.CreateOrUpdateRunResult(race, rr1, rr5r1);
      DBCacheWorkaround();
      rr5r1._participant = participant5 = race.GetParticipants().Where(x => x.Name == "Nachname 5").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr5r1, 5, 1));

      RunResult rr5r2 = new RunResult(participant5);
      rr5r2.ResultCode = RunResult.EResultCode.NaS;
      db.CreateOrUpdateRunResult(race, rr2, rr5r2);
      DBCacheWorkaround();
      rr5r2._participant = participant5 = race.GetParticipants().Where(x => x.Name == "Nachname 5").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr5r2, 5, 2));
    }


    private bool CheckRunResult(string dbFilename, RunResult runResult, int idParticipant, uint raceRun)
    {
      bool bRes = true;

      OleDbConnection conn = new OleDbConnection { ConnectionString = @"Provider=Microsoft.Jet.OLEDB.4.0; Data source= " + dbFilename };
      conn.Open();

      OleDbCommand command = new OleDbCommand("SELECT * FROM tblZeit WHERE teilnehmer = @teilnehmer AND disziplin = @disziplin AND durchgang = @durchgang", conn);
      command.Parameters.Add(new OleDbParameter("@teilnehmer", idParticipant));
      command.Parameters.Add(new OleDbParameter("@disziplin", 2)); // TODO: Add correct disiziplin
      command.Parameters.Add(new OleDbParameter("@durchgang", raceRun));

      // Execute command  
      using (OleDbDataReader reader = command.ExecuteReader())
      {
        if (reader.Read())
        {
          bRes &= (byte) runResult.ResultCode == reader.GetByte(reader.GetOrdinal("ergcode"));

          TimeSpan? runTime = null, startTime = null, finishTime = null;
          if (!reader.IsDBNull(reader.GetOrdinal("netto")))
            runTime = Database.CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("netto")));
          if (!reader.IsDBNull(reader.GetOrdinal("start")))
            startTime = Database.CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("start")));
          if (!reader.IsDBNull(reader.GetOrdinal("ziel")))
            finishTime = Database.CreateTimeSpan((double)reader.GetValue(reader.GetOrdinal("ziel")));

          bRes &= runResult.GetStartTime() == startTime;
          bRes &= runResult.GetFinishTime() == finishTime;
          bRes &= runResult.GetRunTime(false, false) == runTime;

          if (reader.IsDBNull(reader.GetOrdinal("disqualtext")))
            bRes &= runResult.DisqualText == null || runResult.DisqualText == "";
          else
            bRes &= runResult.DisqualText == reader["disqualtext"].ToString();
        }
        else
          bRes = false;
      }

      conn.Close();

      return bRes;
    }


    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void AppDataModelTest_TimingScenario1()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      void DBCacheWorkaround()
      {
        db.Close(); // WORKAROUND: OleDB caches the update, so the Check would not see the changes
        db.Connect(dbFilename);
      }

      AppDataModel model = new AppDataModel(db);
      {

        // Create a RaceRun with 2 runs
        //model.CreateRaceRun(2);
        Race race = model.GetCurrentRace();
        RaceRun rr1 = model.GetCurrentRace().GetRun(0);
        RaceRun rr2 = model.GetCurrentRace().GetRun(1);

        RaceParticipant participant1 = race.GetParticipants().Where(x => x.Participant.Name == "Nachname 1").FirstOrDefault();
        rr1.SetStartTime(participant1, new TimeSpan(0, 12, 0, 0, 0)); // Start
        rr1.SetFinishTime(participant1, new TimeSpan(0, 12, 1, 0, 0)); // Finish


        RaceParticipant participant2 = race.GetParticipants().Where(x => x.Participant.Name == "Nachname 2").FirstOrDefault();
        rr1.SetStartTime(participant2, new TimeSpan(0, 12, 2, 0, 0)); // Start
        rr1.SetFinishTime(participant2, null); // No Finish
                                               // TODO: Set to NiZ

        RaceParticipant participant3 = race.GetParticipants().Where(x => x.Participant.Name == "Nachname 3").FirstOrDefault();
        rr1.SetStartFinishTime(participant3, null, null); // NaS

        RaceParticipant participant4 = race.GetParticipants().Where(x => x.Participant.Name == "Nachname 4").FirstOrDefault();
        rr1.SetStartTime(participant4, new TimeSpan(0, 12, 4, 0, 0)); // Start
        rr1.SetFinishTime(participant4, new TimeSpan(0, 12, 4, 30, 0)); // Finish
        // TODO: Set to Disqualify

      }

      DBCacheWorkaround();

      // Test 1: Check internal app model
      // Test 2: Check whether database is correct
      {
        Race race = model.GetCurrentRace();
        RaceRun rr1 = model.GetCurrentRace().GetRun(0);
        RaceRun rr2 = model.GetCurrentRace().GetRun(1);

        // Participant 1 / Test 1
        RunResult rr1res1 = rr1.GetResultList().Where(x => x._participant.Participant.Name == "Nachname 1").FirstOrDefault();
        Assert.AreEqual(new TimeSpan(0, 12, 0, 0, 0), rr1res1.GetStartTime());
        Assert.AreEqual(new TimeSpan(0, 12, 1, 0, 0), rr1res1.GetFinishTime());
        Assert.AreEqual(new TimeSpan(0,  0, 1, 0, 0), rr1res1.GetRunTime());
        // Participant 1 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res1, 1, 1));

        // Participant 2 / Test 1
        RunResult rr1res2 = rr1.GetResultList().Where(x => x._participant.Participant.Name == "Nachname 2").FirstOrDefault();
        Assert.AreEqual(new TimeSpan(0, 12, 2, 0, 0), rr1res2.GetStartTime());
        Assert.IsNull(rr1res2.GetFinishTime());
        Assert.IsNull(rr1res2.GetRunTime());
        //Assert.Equals(RunResult.EResultCode.NiZ, rr1res2.ResultCode);
        // Participant 2 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res2, 2, 1));

        // Participant 3 / Test 1
        RunResult rr1res3 = rr1.GetResultList().Where(x => x._participant.Participant.Name == "Nachname 3").FirstOrDefault();
        Assert.IsNull(rr1res3.GetStartTime());
        Assert.IsNull(rr1res3.GetFinishTime());
        Assert.IsNull(rr1res3.GetRunTime());
        //Assert.Equals(RunResult.EResultCode.NaS, rr1res3.ResultCode);
        // Participant 3 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res3, 3, 1));

        // Participant 4 / Test 1
        RunResult rr1res4 = rr1.GetResultList().Where(x => x._participant.Participant.Name == "Nachname 4").FirstOrDefault();
        Assert.AreEqual(new TimeSpan(0, 12, 4,  0, 0), rr1res4.GetStartTime());
        Assert.AreEqual(new TimeSpan(0, 12, 4, 30, 0), rr1res4.GetFinishTime());
        Assert.AreEqual(new TimeSpan(0,  0, 0, 30, 0), rr1res4.GetRunTime());
        //Assert.Equals(RunResult.EResultCode.Normal, rr1res4.ResultCode);
        // Participant 4 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res4, 4, 1));
      }
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void AppDataModelTest_EditParticipants()
    {
      string dbFilename = TestUtilities.CreateWorkingFileFrom(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      RaceHorologyLib.Database db = new RaceHorologyLib.Database();
      db.Connect(dbFilename);

      void DBCacheWorkaround()
      {
        db.Close(); // WORKAROUND: OleDB caches the update, so the Check would not see the changes
        db.Connect(dbFilename);
      }

      AppDataModel model = new AppDataModel(db);

      Participant participant1 = db.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
      participant1.Name = "Nachname 1.1";

      Participant participant6 = new Participant
      {
        Name = "Nachname 6",
        Firstname = "Vorname 6",
        Sex = "M",
        Club = "Verein 6",
        Nation = "Nation 6",
        Class = new ParticipantClass("", null, "dummy", "M", 2019, 0),
        Year = 2000,
      };
      model.GetParticipants().Add(participant6);


      DBCacheWorkaround();


      // Test 1: Check whether database is correct
      CheckParticipant(dbFilename, participant1, 1);
      CheckParticipant(dbFilename, participant6, 6);
    }
  }
}
