/*
 *  Copyright (C) 2019 - 2026 by Sven Flossmann & Co-Authors (CREDITS.TXT)
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RaceHorologyLib;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RaceHorologyLibTest
{
  /// <summary>
  /// Summary description for ImportTimeTest
  /// </summary>
  [TestClass]
  public class ImportTimeTest
  {
    public class ImportTimeMock : IImportTime
    {
      public event ImportTimeEntryEventHandler ImportTimeEntryReceived;

      public EImportTimeFlags SupportedImportTimeFlags() { return 0; }
      public void DownloadImportTimes(RaceRun run = null)
      {
      }

      public void TriggerImportTimeEntryReceived(ImportTimeEntry entry)
      {
        ImportTimeEntryReceived.Invoke(this, entry);
      }

    }



    public ImportTimeTest()
    {
      SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
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
    [ClassInitialize()]
    public static void MyClassInitialize(TestContext testContext)
    {
      if (System.Windows.Application.Current == null)
      {
        new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
      }
    }
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
    public void ImportTimeEntryWithParticipant()
    {
      TestDataGenerator tg = new TestDataGenerator();
      var rp = tg.createRaceParticipant();

      ImportTimeEntry ie1 = new ImportTimeEntry(1U, new TimeSpan(0, 0, 10));

      ImportTimeEntryWithParticipant entry1 = new ImportTimeEntryWithParticipant(ie1, tg.Model.GetRace(0));
      Assert.AreEqual(1U, entry1.StartNumber);
      Assert.AreEqual("Name 1", entry1.Name);

      ImportTimeEntry ie2 = new ImportTimeEntry(99999U, new TimeSpan(0, 0, 10));
      // ImportTimeEntryWithParticipant and no valid startnumber participant
      ImportTimeEntryWithParticipant entry2 = new ImportTimeEntryWithParticipant(ie2, tg.Model.GetRace(0));
      Assert.AreEqual(99999U, entry2.StartNumber);
      Assert.AreEqual(null, entry2.Name);
    }


    [TestMethod]
    public void ImportTimeEntryVM_RunTime()
    {
      TestDataGenerator tg = new TestDataGenerator();
      tg.createRaceParticipants(5);
      var race = tg.Model.GetRace(0);

      ImportTimeMock importTimeMock = new ImportTimeMock();

      ImportTimeEntryVM vm = new ImportTimeEntryVM(race, importTimeMock);
      vm.AddEntry(new ImportTimeEntry (1, new TimeSpan(0, 0, 10)));

      Assert.AreEqual(1, vm.ImportEntries.Count);
      Assert.AreEqual(1U, vm.ImportEntries[0].StartNumber);
      Assert.AreEqual(new TimeSpan(0,0,0,10), vm.ImportEntries[0].RunTime);

      vm.AddEntry(new ImportTimeEntry (3, new TimeSpan(0, 0, 13)));
      Assert.AreEqual(2, vm.ImportEntries.Count);
      Assert.AreEqual(1U, vm.ImportEntries[0].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 10), vm.ImportEntries[0].RunTime);
      Assert.AreEqual(3U, vm.ImportEntries[1].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 13), vm.ImportEntries[1].RunTime);

      // Update startnumber 1
      importTimeMock.TriggerImportTimeEntryReceived(new ImportTimeEntry (1, new TimeSpan(0, 0, 11)));
      Assert.AreEqual(2, vm.ImportEntries.Count);
      Assert.AreEqual(1U, vm.ImportEntries[0].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 11), vm.ImportEntries[0].RunTime);
      Assert.AreEqual(3U, vm.ImportEntries[1].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 13), vm.ImportEntries[1].RunTime);

      // Add entry without participant
      importTimeMock.TriggerImportTimeEntryReceived(new ImportTimeEntry(999, new TimeSpan(0, 0, 9)));
      Assert.AreEqual(3, vm.ImportEntries.Count);
      Assert.AreEqual(1U, vm.ImportEntries[0].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 11), vm.ImportEntries[0].RunTime);
      Assert.AreEqual(3U, vm.ImportEntries[1].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 13), vm.ImportEntries[1].RunTime);
      Assert.AreEqual(999U, vm.ImportEntries[2].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 9), vm.ImportEntries[2].RunTime);

      // Add second entry without participant
      importTimeMock.TriggerImportTimeEntryReceived(new ImportTimeEntry(998, new TimeSpan(0, 0, 8)));
      Assert.AreEqual(4, vm.ImportEntries.Count);
      Assert.AreEqual(1U, vm.ImportEntries[0].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 11), vm.ImportEntries[0].RunTime);
      Assert.AreEqual(3U, vm.ImportEntries[1].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 13), vm.ImportEntries[1].RunTime);
      Assert.AreEqual(999U, vm.ImportEntries[2].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 9), vm.ImportEntries[2].RunTime);
      Assert.AreEqual(998U, vm.ImportEntries[3].StartNumber);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 8), vm.ImportEntries[3].RunTime);


      // Save to racerun, only time for real articipants should be taken over
      // StNr 1, 3 have time
      // StNr 2 doesn't have a time
      var rr1 = race.GetRun(0);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(1))?.Runtime);

      var save = new List<ImportTimeEntryWithParticipant>();
      save.Add(vm.ImportEntries[0]);
      vm.Save(rr1, save, false);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 11), rr1.GetRunResult(race.GetParticipant(1)).Runtime);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(1)).StartTime);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(1)).FinishTime);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(2))?.Runtime);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(3))?.Runtime);

      save.Clear();
      save.Add(vm.ImportEntries[1]);
      save.Add(vm.ImportEntries[2]);
      save.Add(vm.ImportEntries[3]);
      vm.Save(rr1, save, false);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 11), rr1.GetRunResult(race.GetParticipant(1)).Runtime);
      Assert.AreEqual(null, rr1.GetRunResult(race.GetParticipant(2))?.Runtime);
      Assert.AreEqual(new TimeSpan(0, 0, 0, 13), rr1.GetRunResult(race.GetParticipant(3)).Runtime);
    }

    [TestMethod]
    public void ImportTimeEntryVM_StartFinishTime()
    {
      TestDataGenerator tg = new TestDataGenerator();
      tg.createRaceParticipants(5);
      var race = tg.Model.GetRace(0);

      ImportTimeMock importTimeMock = new ImportTimeMock();

      ImportTimeEntryVM vm = new ImportTimeEntryVM(race, importTimeMock);
      vm.AddEntry(new ImportTimeEntry(1, new TimeSpan(8, 0, 1), null));
      vm.AddEntry(new ImportTimeEntry(1, null, new TimeSpan(8, 0, 2)));

      vm.AddEntry(new ImportTimeEntry(3, new TimeSpan(8, 2, 0), new TimeSpan(8, 2, 30)));
      Assert.AreEqual(2, vm.ImportEntries.Count);

      vm.AddEntry(new ImportTimeEntry(0, new TimeSpan(8, 0, 1), null));
      vm.AddEntry(new ImportTimeEntry(0, null, new TimeSpan(8, 0, 2)));
      Assert.AreEqual(4, vm.ImportEntries.Count);

      // Save to racerun, only time for real participants should be taken over
      // StNr 1, 3 have time
      // StNr 2 doesn't have a time
      var rr1 = race.GetRun(0);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(1))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(1))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(1))?.FinishTime == null);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(2))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(2))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(2))?.FinishTime == null);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(3))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(3))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(3))?.FinishTime == null);

      var save = new List<ImportTimeEntryWithParticipant>();
      save.Add(vm.ImportEntries[0]);
      vm.Save(rr1, save, false);
      Assert.AreEqual(new TimeSpan(8, 0, 1), rr1.GetRunResult(race.GetParticipant(1)).StartTime);
      Assert.AreEqual(new TimeSpan(8, 0, 2), rr1.GetRunResult(race.GetParticipant(1)).FinishTime);
      Assert.AreEqual(new TimeSpan(0, 0, 1), rr1.GetRunResult(race.GetParticipant(1)).Runtime);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(2))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(2))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(2))?.FinishTime == null);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(3))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(3))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(3))?.FinishTime == null);

      save.Clear();
      save.Add(vm.ImportEntries[1]);
      save.Add(vm.ImportEntries[2]);
      save.Add(vm.ImportEntries[3]);
      vm.Save(rr1, save, false);

      Assert.AreEqual(new TimeSpan(8, 0, 1), rr1.GetRunResult(race.GetParticipant(1)).StartTime);
      Assert.AreEqual(new TimeSpan(8, 0, 2), rr1.GetRunResult(race.GetParticipant(1)).FinishTime);
      Assert.AreEqual(new TimeSpan(0, 0, 1), rr1.GetRunResult(race.GetParticipant(1)).Runtime);
      Assert.IsTrue(rr1.GetRunResult(race.GetParticipant(2))?.Runtime == null && rr1.GetRunResult(race.GetParticipant(2))?.StartTime == null && rr1.GetRunResult(race.GetParticipant(2))?.FinishTime == null);
      Assert.AreEqual(new TimeSpan(8, 2, 0), rr1.GetRunResult(race.GetParticipant(3)).StartTime);
      Assert.AreEqual(new TimeSpan(8, 2, 30), rr1.GetRunResult(race.GetParticipant(3)).FinishTime);
      Assert.AreEqual(new TimeSpan(0, 0, 30), rr1.GetRunResult(race.GetParticipant(3)).Runtime);
    }


    /// <summary>
    /// After a bulk Save the result view must end up correctly sorted, even though resorting
    /// was suspended while the individual times were applied.
    /// </summary>
    [TestMethod]
    public void ImportTimeEntryVM_Save_ResortsOnceAndStaysSorted()
    {
      TestDataGenerator tg = new TestDataGenerator();
      tg.createRaceParticipants(3);
      var race = tg.Model.GetRace(0);
      var rr1 = race.GetRun(0);
      var vp = rr1.GetResultViewProvider();

      ImportTimeMock importTimeMock = new ImportTimeMock();
      ImportTimeEntryVM vm = new ImportTimeEntryVM(race, importTimeMock);

      // Distinct start times (timestamps must be unique), distinct net runtimes:
      // StNr 1 -> 30s, StNr 2 -> 10s (fastest), StNr 3 -> 20s
      vm.AddEntry(new ImportTimeEntry(1, new TimeSpan(8, 0, 0), new TimeSpan(8, 0, 30)));
      vm.AddEntry(new ImportTimeEntry(2, new TimeSpan(8, 1, 0), new TimeSpan(8, 1, 10)));
      vm.AddEntry(new ImportTimeEntry(3, new TimeSpan(8, 2, 0), new TimeSpan(8, 2, 20)));
      Assert.AreEqual(3, vm.ImportEntries.Count);

      var save = new List<ImportTimeEntryWithParticipant>
      {
        vm.ImportEntries[0],
        vm.ImportEntries[1],
        vm.ImportEntries[2],
      };
      var count = vm.Save(rr1, save, false);
      Assert.AreEqual(3, count);

      // Times were applied
      Assert.AreEqual(new TimeSpan(0, 0, 30), rr1.GetRunResult(race.GetParticipant(1)).Runtime);
      Assert.AreEqual(new TimeSpan(0, 0, 10), rr1.GetRunResult(race.GetParticipant(2)).Runtime);
      Assert.AreEqual(new TimeSpan(0, 0, 20), rr1.GetRunResult(race.GetParticipant(3)).Runtime);

      // The view ends up sorted by runtime (fastest first) with correct positions
      var view = vp.GetView().ViewToList<RunResultWithPosition>();
      Assert.AreEqual(2U, view[0].StartNumber);
      Assert.AreEqual(1U, view[0].Position);
      Assert.AreEqual(3U, view[1].StartNumber);
      Assert.AreEqual(2U, view[1].Position);
      Assert.AreEqual(1U, view[2].StartNumber);
      Assert.AreEqual(3U, view[2].Position);
    }


    /// <summary>
    /// Suspension is nestable; the deferred resort only happens once the outermost resume occurs.
    /// </summary>
    [TestMethod]
    public void ViewProvider_SuspendResorting_IsNestable()
    {
      TestDataGenerator tg = new TestDataGenerator();
      tg.createRaceParticipants(3);
      var race = tg.Model.GetRace(0);
      var rr1 = race.GetRun(0);
      var vp = rr1.GetResultViewProvider();

      vp.SuspendResorting();
      vp.SuspendResorting();

      // While suspended, applying a faster time to a higher start number must not yet reorder.
      rr1.SetRunTime(race.GetParticipant(3), new TimeSpan(0, 0, 5));
      var viewWhileSuspended = vp.GetView().ViewToList<RunResultWithPosition>();
      Assert.AreEqual(1U, viewWhileSuspended[0].StartNumber, "view reordered while still suspended");

      vp.ResumeResorting(); // inner resume: still suspended
      var viewAfterInnerResume = vp.GetView().ViewToList<RunResultWithPosition>();
      Assert.AreEqual(1U, viewAfterInnerResume[0].StartNumber, "view reordered before outermost resume");

      vp.ResumeResorting(); // outermost resume: resort happens now
      var viewAfterResume = vp.GetView().ViewToList<RunResultWithPosition>();
      Assert.AreEqual(3U, viewAfterResume[0].StartNumber);
      Assert.AreEqual(1U, viewAfterResume[0].Position);
    }


    /// <summary>
    /// An imported result-code entry (e.g. DNS) must set the result code on the participant's run result.
    /// </summary>
    [TestMethod]
    public void ImportTimeEntryVM_Save_ResultCode_DNS()
    {
      TestDataGenerator tg = new TestDataGenerator();
      tg.createRaceParticipants(3);
      var race = tg.Model.GetRace(0);
      var rr1 = race.GetRun(0);

      ImportTimeMock importTimeMock = new ImportTimeMock();
      ImportTimeEntryVM vm = new ImportTimeEntryVM(race, importTimeMock);

      // StNr 2 did not start
      vm.AddEntry(new ImportTimeEntry(2, RunResult.EResultCode.NaS));
      Assert.AreEqual(1, vm.ImportEntries.Count);
      Assert.AreEqual(RunResult.EResultCode.NaS, vm.ImportEntries[0].ResultCode);
      Assert.AreEqual("NaS", vm.ImportEntries[0].Status);

      var save = new List<ImportTimeEntryWithParticipant> { vm.ImportEntries[0] };
      var count = vm.Save(rr1, save, false);

      Assert.AreEqual(1, count);
      Assert.AreEqual(RunResult.EResultCode.NaS, rr1.GetRunResult(race.GetParticipant(2)).ResultCode);
      Assert.IsNull(rr1.GetRunResult(race.GetParticipant(2)).Runtime);
    }
  }
}
