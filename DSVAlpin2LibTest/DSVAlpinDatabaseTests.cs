﻿using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSVAlpin2Lib;
using System.IO;
using System.Data.OleDb;
using System.Linq;

namespace DSVAlpin2LibTest
{
  /// <summary>
  /// Summary description for DSVAlpinDatabaseTests
  /// </summary>
  [TestClass]
  public class DSVAlpinDatabaseTests
  {
    public DSVAlpinDatabaseTests()
    {
      //
      // TODO: Add constructor logic here
      //
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
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
      db.Connect(Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb"));

      var participants = db.GetParticipants();

      Assert.IsTrue(participants.Count() == 5);
      Assert.IsTrue(participants.Where(x => x.Name == "Nachname 3").Count() == 1);

      db.Close();
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_LessParticipants.mdb")]
    public void DatabaseRaceRuns()
    {
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
      db.Connect(Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb"));

      db.GetParticipants();
      var rr1 = db.GetRaceRun(1);
      var rr2 = db.GetRaceRun(2);

      Assert.IsTrue(rr1.Count() == 4);
      Assert.IsTrue(rr2.Count() == 4);

      Assert.IsTrue(rr1.Where(x => x.GetFinishTime() == null && x.GetStartTime() != null).First().Participant.Name == "Nachname 3");

      Assert.IsTrue(rr2.Where(x => x.GetFinishTime() == null && x.GetStartTime() != null).First().Participant.Name == "Nachname 2");

      Assert.IsTrue(rr2.Where(x => x.Participant.Name == "Nachname 5").Count() == 0);

      db.Close();
    }


    [TestMethod]
    //[DeploymentItem(@"TestDataBases\KSC2019-2-PSL.mdb")]
    [DeploymentItem(@"TestDataBases\Kirchberg U8 U10 10.02.19 RS Neu.mdb")]
    public void InitializeApplicationModel()
    {
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
      //db.Connect(Path.Combine(testContextInstance.TestDeploymentDir, @"KSC2019-2-PSL.mdb"));
      db.Connect(Path.Combine(testContextInstance.TestDeploymentDir, @"Kirchberg U8 U10 10.02.19 RS Neu.mdb"));

      AppDataModel model = new AppDataModel(db);
    }

    [TestMethod]
    [DeploymentItem(@"TestDataBases\TestDB_Empty.mdb")]
    public void CreateAndUpdateParticipants()
    {
      string dbFilename = Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_Empty.mdb");
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
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
        Class = "Testklasse 1",
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
        Class = "Testklasse 1",
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
        Class = "Testklasse 1",
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
      pNew1.Class = "Testklasse 1.1";
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
      pNew1.Class = "Testklasse 1.1";
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
      string dbFilename = Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
      db.Connect(dbFilename);

      var participants = db.GetParticipants();

      void DBCacheWorkaround()
      {
        db.Close(); // WORKAROUND: OleDB caches the update, so the Check would not see the changes
        db.Connect(dbFilename);
        participants = db.GetParticipants();
      }

      RaceRun rr1 = new RaceRun(1);
      RaceRun rr2 = new RaceRun(2);

      Participant participant1 = participants.Where(x => x.Name == "Nachname 1").FirstOrDefault();
      RunResult rr1r1 = new RunResult();
      rr1r1._participant = participant1;

      rr1r1.SetStartFinishTime(new TimeSpan(0, 12, 0, 0, 0), null); //int days, int hours, int minutes, int seconds, int milliseconds
      db.CreateOrUpdateRunResult(rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = participants.Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      rr1r1.SetStartFinishTime(rr1r1.GetStartTime(), new TimeSpan(0, 12, 1, 0, 0)); //int days, int hours, int minutes, int seconds, int milliseconds
      db.CreateOrUpdateRunResult(rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = participants.Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      rr1r1.ResultCode = RunResult.EResultCode.DIS;
      rr1r1.DisqualText = "TF Tor 9";
      db.CreateOrUpdateRunResult(rr1, rr1r1);
      DBCacheWorkaround();
      rr1r1._participant = participant1 = participants.Where(x => x.Name == "Nachname 1").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr1r1, 1, 1));

      Participant participant5 = participants.Where(x => x.Name == "Nachname 5").FirstOrDefault();
      RunResult rr5r1 = new RunResult();
      rr5r1._participant = participant5;
      rr5r1.SetStartFinishTime(new TimeSpan(0, 12, 1, 1, 1), null); //int days, int hours, int minutes, int seconds, int milliseconds
      rr5r1.ResultCode = RunResult.EResultCode.NiZ;
      db.CreateOrUpdateRunResult(rr1, rr5r1);
      DBCacheWorkaround();
      rr5r1._participant = participant5 = participants.Where(x => x.Name == "Nachname 5").FirstOrDefault();
      Assert.IsTrue(CheckRunResult(dbFilename, rr5r1, 5, 1));

      RunResult rr5r2 = new RunResult();
      rr5r2._participant = participant5;
      rr5r2.ResultCode = RunResult.EResultCode.NaS;
      db.CreateOrUpdateRunResult(rr2, rr5r2);
      DBCacheWorkaround();
      rr5r2._participant = participant5 = participants.Where(x => x.Name == "Nachname 5").FirstOrDefault();
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
          bRes &= runResult.GetRunTime() == runTime;

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
      string dbFilename = Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
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
        RaceRun rr1 = model.GetRun(0);
        RaceRun rr2 = model.GetRun(1);

        Participant participant1 = db.GetParticipants().Where(x => x.Name == "Nachname 1").FirstOrDefault();
        rr1.SetTimeMeasurement(participant1, new TimeSpan(0, 12, 0, 0, 0), null); // Start
        rr1.SetTimeMeasurement(participant1, new TimeSpan(0, 12, 0, 0, 0), new TimeSpan(0, 12, 1, 0, 0)); // Finish


        Participant participant2 = db.GetParticipants().Where(x => x.Name == "Nachname 2").FirstOrDefault();
        rr1.SetTimeMeasurement(participant2, new TimeSpan(0, 12, 2, 0, 0), null); // Start
                                                                               // TODO: Set to NiZ

        Participant participant3 = db.GetParticipants().Where(x => x.Name == "Nachname 3").FirstOrDefault();
        rr1.SetTimeMeasurement(participant3, null, null); // NaS

        Participant participant4 = db.GetParticipants().Where(x => x.Name == "Nachname 4").FirstOrDefault();
        rr1.SetTimeMeasurement(participant4, new TimeSpan(0, 12, 4, 0, 0), null); // Start
        rr1.SetTimeMeasurement(participant4, new TimeSpan(0, 12, 4, 0, 0), new TimeSpan(0, 12, 4, 30, 0)); // Finish
        // TODO: Set to Disqualify

      }

      DBCacheWorkaround();

      // Test 1: Check internal app model
      // Test 2: Check whether database is correct
      {
        RaceRun rr1 = model.GetRun(0);
        RaceRun rr2 = model.GetRun(1);

        // Participant 1 / Test 1
        RunResult rr1res1 = rr1.GetResultList().Where(x => x._participant.Name == "Nachname 1").FirstOrDefault();
        Assert.AreEqual(new TimeSpan(0, 12, 0, 0, 0), rr1res1.GetStartTime());
        Assert.AreEqual(new TimeSpan(0, 12, 1, 0, 0), rr1res1.GetFinishTime());
        Assert.AreEqual(new TimeSpan(0,  0, 1, 0, 0), rr1res1.GetRunTime());
        // Participant 1 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res1, 1, 1));

        // Participant 2 / Test 1
        RunResult rr1res2 = rr1.GetResultList().Where(x => x._participant.Name == "Nachname 2").FirstOrDefault();
        Assert.AreEqual(new TimeSpan(0, 12, 2, 0, 0), rr1res2.GetStartTime());
        Assert.IsNull(rr1res2.GetFinishTime());
        Assert.IsNull(rr1res2.GetRunTime());
        //Assert.Equals(RunResult.EResultCode.NiZ, rr1res2.ResultCode);
        // Participant 2 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res2, 2, 1));

        // Participant 3 / Test 1
        RunResult rr1res3 = rr1.GetResultList().Where(x => x._participant.Name == "Nachname 3").FirstOrDefault();
        Assert.IsNull(rr1res3.GetStartTime());
        Assert.IsNull(rr1res3.GetFinishTime());
        Assert.IsNull(rr1res3.GetRunTime());
        //Assert.Equals(RunResult.EResultCode.NaS, rr1res3.ResultCode);
        // Participant 3 / Test 2
        Assert.IsTrue(CheckRunResult(dbFilename, rr1res3, 3, 1));

        // Participant 4 / Test 1
        RunResult rr1res4 = rr1.GetResultList().Where(x => x._participant.Name == "Nachname 4").FirstOrDefault();
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
      string dbFilename = Path.Combine(testContextInstance.TestDeploymentDir, @"TestDB_LessParticipants.mdb");
      DSVAlpin2Lib.Database db = new DSVAlpin2Lib.Database();
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
        Class = "unknown",
        Year = 2000,
        StartNumber = 999
      };
      model.GetParticipants().Add(participant6);


      DBCacheWorkaround();


      // Test 1: Check whether database is correct
      CheckParticipant(dbFilename, participant1, 1);
      CheckParticipant(dbFilename, participant6, 6);
    }

  }
  }