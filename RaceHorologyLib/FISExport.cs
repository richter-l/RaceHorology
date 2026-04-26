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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace RaceHorologyLib
{

  public class FISExportException : Exception
  {
    public FISExportException(string message) : base(message)
    { }

    public string GetHumanReadableError()
    {
      switch (Message)
      {
        case "missing racedate":
          return "Renndatum fehlt";
        case "missing raceid":
          return "Rennnummer fehlt";
        case "missing raceorganizer":
          return "Rennorganisator fehlt";
        case "missing racename":
          return "Rennname und/oder Beschreibung fehlt";
        case "missing raceplace":
          return "Rennort fehlt";
        case "not supported racetype":
          return "Renntyp nicht unterstützt";
        case "missing racejury Chiefrace":
          return "Rennleiter fehlt";
        case "missing racejury Referee":
          return "Schiedsrichter fehlt";
        case "missing racejury TD":
          return "Trainervertreter/TD fehlt";
        case "missing racejury Assistantreferee":
          return "Schiedsrichterassistent fehlt";
        case "missing coursesetter":
          return "Kurssetzer fehlt";
        case "missing coarsename":
          return "Kursname fehlt";
        case "missing number_of_gates":
          return "Anzahl der Tore fehlt";
        case "missing number_of_turninggates":
          return "Anzahl der Richtungsänderungen fehlt";
        case "missing startaltitude":
          return "Starthöhe fehlt";
        case "missing finishaltitude":
          return "Zielhöhe fehlt";
        case "missing courselength":
          return "Kurslänge fehlt";
        case "missing forerunner":
          return "Vorläufer fehlt";
        case "missing f-value":
          return "F-Wert nicht korrekt";
        default:
          return Message;
      }
    }
  }


  public class FISExport
  {
    protected XmlWriterSettings _xmlSettings;
    protected XmlWriter _writer;
    protected bool _writePoints;

    public FISExport()
    {
      _xmlSettings = new XmlWriterSettings();
      _xmlSettings.Indent = true;
      _xmlSettings.IndentChars = "  ";
      _xmlSettings.Encoding = Encoding.UTF8;
    }


    public void Export(string pathZIPFile, Race race)
    {
      void addImageToZip(ZipArchive archive, string imageSrcName, string imageName, Race raceInternal)
      {
        PDFHelper pdfHelper = new PDFHelper(raceInternal.GetDataModel());
        string imgSrcPath = pdfHelper.FindImage(imageSrcName);
        if (imgSrcPath != null)
        {
          var imgZipFile = archive.CreateEntry(imageName + ".jpg");
          using (var imgZipStream = imgZipFile.Open())
          {
            Image img = Image.FromFile(imgSrcPath);
            img.Save(imgZipStream, ImageFormat.Jpeg);
            imgZipStream.Close();
          }
        }
      }

      string baseFileName = Path.GetFileNameWithoutExtension(pathZIPFile);

      using (var zipStream = new MemoryStream())
      {
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
          var xmlFile = archive.CreateEntry(baseFileName + ".xml");
          using (var xmlStream = xmlFile.Open())
          {
            ExportXML(xmlStream, race);
          }

          addImageToZip(archive, "Logo1", "LogoClub", race);
        }

        using (var fileStream = new FileStream(pathZIPFile, FileMode.Create))
        {
          zipStream.Seek(0, SeekOrigin.Begin);
          zipStream.CopyTo(fileStream);
        }
      }
    }

    public void ExportXML(string pathXMLFile, Race race)
    {
      FileStream output = new FileStream(pathXMLFile, FileMode.Create);
      ExportXML(output, race);
    }


    public void ExportXML(Stream output, Race race)
    {
      _writePoints = race.RaceConfiguration.ActiveFields.Contains("Points");

      _writer = XmlWriter.Create(output, _xmlSettings);

      _writer.WriteStartDocument();
      writeRace(race);
      _writer.WriteEndDocument();

      _writer.Close();
    }


    void writeRace(Race race)
    {
      _writer.WriteStartElement("Fisresults");
      writeRaceheader(race);
      writeALRaceinfo(race);
      writeRaceResults(race);
      _writer.WriteEndElement();
    }


    void writeRaceheader(Race race)
    {
      _writer.WriteStartElement("Raceheader");

      if (race.DateResultList == null)
        throw new FISExportException("missing racedate");
      writeElement("Racedate", race.DateResultList?.ToString("yyyy-MM-dd"));

      writeElement("Gender", "A");

      writeElement("Season", race.DateResultList?.AddMonths(3).ToString("yyyy"));

      if (string.IsNullOrWhiteSpace(race.RaceNumber))
        throw new FISExportException("missing raceid");
      writeElement("Raceid", race.RaceNumber);

      if (string.IsNullOrWhiteSpace(race.AdditionalProperties?.Organizer))
        throw new FISExportException("missing raceorganizer");
      writeElement("Raceorganizer", race.AdditionalProperties?.Organizer);

      if (!string.IsNullOrWhiteSpace(race.AdditionalProperties?.RaceNation))
        writeElement("Nation", race.AdditionalProperties.RaceNation);

      writeElement("Discipline", getFISDiscipline(race));

      if (!string.IsNullOrWhiteSpace(race.AdditionalProperties?.FISCategory))
        writeElement("Category", race.AdditionalProperties.FISCategory);

      if (string.IsNullOrWhiteSpace(race.Description))
        throw new FISExportException("missing racename");
      writeElement("Racename", race.Description);

      if (string.IsNullOrWhiteSpace(race.AdditionalProperties?.Location))
        throw new FISExportException("missing raceplace");
      writeElement("Raceplace", race.AdditionalProperties?.Location);

      writeElement("Timing", string.IsNullOrWhiteSpace(race.TimingDevice) ? "Race Horology" : race.TimingDevice);

      if (!string.IsNullOrEmpty(race.AdditionalProperties?.Analyzer.Club) && !string.IsNullOrEmpty(race.AdditionalProperties?.Analyzer.Name))
        writeElement("Dataprocessing_by", string.Format("{0}, {1}", race.AdditionalProperties?.Analyzer.Name, race.AdditionalProperties?.Analyzer.Club));
      else if (!string.IsNullOrEmpty(race.AdditionalProperties?.Analyzer.Club))
        writeElement("Dataprocessing_by", race.AdditionalProperties?.Analyzer.Club);
      else if (!string.IsNullOrEmpty(race.AdditionalProperties?.Analyzer.Name))
        writeElement("Dataprocessing_by", race.AdditionalProperties?.Analyzer.Name);

      string software = getSoftware();
      if (software != null)
        writeElement("Software", software);

      _writer.WriteEndElement();
    }


    void writeALRaceinfo(Race race)
    {
      _writer.WriteStartElement("AL_raceinfo");

      FISRaceResultViewProvider fisRaceVP = race.GetResultViewProvider() as FISRaceResultViewProvider;
      if (fisRaceVP != null)
      {
        if (race.RaceConfiguration.ValueF > 0.0)
          writeElement("Fvalue", race.RaceConfiguration.ValueF.ToString("F0", CultureInfo.InvariantCulture));

        FISRaceCalculation fisCalc = fisRaceVP.GetFISRaceCalculation();
        if (fisCalc != null && fisCalc.CalculationValid)
        {
          writeElement("Appliedpenalty", fisCalc.AppliedPenalty.ToString("F2", CultureInfo.InvariantCulture));
          writeElement("Calculatedpenalty", fisCalc.CalculatedPenalty.ToString("F2", CultureInfo.InvariantCulture));
        }
      }

      if (race.AdditionalProperties?.RaceManager == null || ((AdditionalRaceProperties.Person)race.AdditionalProperties?.RaceManager).IsEmpty())
        throw new FISExportException("missing racejury Chiefrace");
      writeJury("Chiefrace", race.AdditionalProperties?.RaceManager);

      if (race.AdditionalProperties?.RaceReferee == null || ((AdditionalRaceProperties.Person)race.AdditionalProperties?.RaceReferee).IsEmpty())
        throw new FISExportException("missing racejury Referee");
      writeJury("Referee", race.AdditionalProperties?.RaceReferee);

      if (race.RaceType == Race.ERaceType.SuperG || race.RaceType == Race.ERaceType.DownHill)
      {
        if (race.AdditionalProperties?.AssistantReferee == null || ((AdditionalRaceProperties.Person)race.AdditionalProperties?.AssistantReferee).IsEmpty())
          throw new FISExportException("missing racejury Assistantreferee");
      }
      if (race.AdditionalProperties?.AssistantReferee != null && !((AdditionalRaceProperties.Person)race.AdditionalProperties?.AssistantReferee).IsEmpty())
        writeJury("Assistantreferee", race.AdditionalProperties?.AssistantReferee);

      if (race.AdditionalProperties?.TrainerRepresentative == null || ((AdditionalRaceProperties.Person)race.AdditionalProperties?.TrainerRepresentative).IsEmpty())
        throw new FISExportException("missing racejury TD");
      writeJury("TD", race.AdditionalProperties?.TrainerRepresentative);

      writeRuninfo(race.GetRun(0), race.AdditionalProperties?.RaceRun1);
      if (race.GetMaxRun() > 1)
        writeRuninfo(race.GetRun(1), race.AdditionalProperties?.RaceRun2);

      _writer.WriteEndElement();
    }


    void writeJury(string function, AdditionalRaceProperties.Person person)
    {
      string lastname, firstname;
      DSVExport.guessLastAndFirstname(person?.Name, out lastname, out firstname);

      _writer.WriteStartElement("Jury");
      _writer.WriteAttributeString("Function", function);
      writeElement("Jurylastname", lastname);
      writeElement("Juryfirstname", firstname);
      if (!string.IsNullOrEmpty(person?.Club))
        writeElement("Jurynation", person.Club);
      _writer.WriteEndElement();
    }


    void writeRuninfo(RaceRun raceRun, AdditionalRaceProperties.RaceRunProperties raceRunProperties)
    {
      _writer.WriteStartElement("Runinfo");
      _writer.WriteAttributeString("Run", raceRun.Run.ToString());

      if (string.IsNullOrEmpty(raceRun.GetRace().AdditionalProperties?.CoarseName))
        throw new FISExportException("missing coarsename");
      writeElement("Coursename", raceRun.GetRace().AdditionalProperties?.CoarseName);

      if (!string.IsNullOrEmpty(raceRun.GetRace().AdditionalProperties?.CoarseHomologNo))
        writeElement("Homologationnumber", raceRun.GetRace().AdditionalProperties.CoarseHomologNo);

      if (raceRunProperties.Gates <= 0)
        throw new FISExportException("missing number_of_gates");
      writeElement("Numberofgates", raceRunProperties.Gates.ToString());

      if (raceRunProperties.Turns <= 0)
        throw new FISExportException("missing number_of_turninggates");
      writeElement("Numberofturninggates", raceRunProperties.Turns.ToString());

      if (raceRun.GetRace().AdditionalProperties?.StartHeight <= 0)
        throw new FISExportException("missing startaltitude");
      writeElement("Startaltitude", raceRun.GetRace().AdditionalProperties?.StartHeight.ToString());

      if (raceRun.GetRace().AdditionalProperties?.FinishHeight <= 0)
        throw new FISExportException("missing finishaltitude");
      writeElement("Finishaltitude", raceRun.GetRace().AdditionalProperties?.FinishHeight.ToString());

      if (raceRun.GetRace().AdditionalProperties?.CoarseLength <= 0)
        throw new FISExportException("missing courselength");
      writeElement("Courselength", raceRun.GetRace().AdditionalProperties.CoarseLength.ToString());

      writeCoursesetter(raceRunProperties.CoarseSetter);

      if (!string.IsNullOrWhiteSpace(raceRunProperties.Forerunner1.Name))
        writeForerunner(1, raceRunProperties.Forerunner1);
      else
        throw new FISExportException("missing forerunner");
      if (!string.IsNullOrWhiteSpace(raceRunProperties.Forerunner2.Name))
        writeForerunner(2, raceRunProperties.Forerunner2);
      if (!string.IsNullOrWhiteSpace(raceRunProperties.Forerunner3.Name))
        writeForerunner(3, raceRunProperties.Forerunner3);

      writeElement("Starttime", raceRunProperties.StartTime);

      _writer.WriteEndElement();
    }


    void writeCoursesetter(AdditionalRaceProperties.Person person)
    {
      if (person == null || person.IsEmpty())
        throw new FISExportException("missing coursesetter");

      string lastname, firstname;
      DSVExport.guessLastAndFirstname(person?.Name, out lastname, out firstname);

      _writer.WriteStartElement("Coursesetter");
      writeElement("Lastname", lastname);
      if (!string.IsNullOrEmpty(firstname))
        writeElement("Firstname", firstname);
      if (!string.IsNullOrEmpty(person?.Club))
        writeElement("Nation", person.Club);
      _writer.WriteEndElement();
    }


    void writeForerunner(int order, AdditionalRaceProperties.Person person)
    {
      string lastname, firstname;
      DSVExport.guessLastAndFirstname(person?.Name, out lastname, out firstname);

      _writer.WriteStartElement("Forerunner");
      _writer.WriteAttributeString("Order", order.ToString());
      writeElement("Lastname", lastname);
      if (!string.IsNullOrEmpty(firstname))
        writeElement("Firstname", firstname);
      if (!string.IsNullOrEmpty(person?.Club))
        writeElement("Nation", person.Club);
      _writer.WriteEndElement();
    }


    void writeRaceResults(Race race)
    {
      List<RaceResultItem> classified = new List<RaceResultItem>();
      List<RaceResultItem> notClassified = new List<RaceResultItem>();

      var results = race.GetResultViewProvider().GetView();

      foreach (var result in results.SourceCollection)
      {
        RaceResultItem item = result as RaceResultItem;

        if (item.ResultCode == RunResult.EResultCode.Normal && item.TotalTime != null)
          classified.Add(item);
        else
          notClassified.Add(item);
      }

      writeALClassified(classified);
      writeALNotClassified(notClassified);
    }


    void writeALClassified(List<RaceResultItem> classified)
    {
      _writer.WriteStartElement("AL_classified");

      foreach (RaceResultItem rri in classified)
      {
        _writer.WriteStartElement("AL_ranked");
        _writer.WriteAttributeString("Status", "QLF");

        writeElement("Rank", rri.Position.ToString());
        writeElement("Bib", rri.Participant.StartNumber.ToString());
        writeCompetitor(rri.Participant);

        foreach (var x in rri.SubResults.OrderBy(k => k.Key))
        {
          writeElement("Timerun" + x.Key, formatTime(x.Value.Runtime));
        }

        writeElement("Totaltime", formatTime(rri.TotalTime));

        if (_writePoints && rri.Points >= 0)
          writeElement("Racepoints", rri.Points.ToString("F2", CultureInfo.InvariantCulture));

        _writer.WriteEndElement();
      }

      _writer.WriteEndElement();
    }


    void writeALNotClassified(List<RaceResultItem> notClassified)
    {
      _writer.WriteStartElement("AL_notclassified");

      foreach (RaceResultItem rri in notClassified)
      {
        string status;
        uint failedRun;
        mapNotClassifiedStatus(rri, out status, out failedRun);

        _writer.WriteStartElement("AL_notranked");
        _writer.WriteAttributeString("Status", status);

        writeElement("Bib", rri.Participant.StartNumber.ToString());
        writeCompetitor(rri.Participant);

        if (failedRun > 0)
          writeElement("Run", failedRun.ToString());

        if (!string.IsNullOrWhiteSpace(rri.DisqualText))
          writeElement("Reason", rri.DisqualText);

        _writer.WriteEndElement();
      }

      _writer.WriteEndElement();
    }


    void writeCompetitor(RaceParticipant participant)
    {
      _writer.WriteStartElement("Competitor");

      writeElement("Fiscode", participant.Participant.CodeOrSvId);

      writeElement("Lastname", participant.Name);
      writeElement("Firstname", participant.Firstname);

      writeElement("Year", participant.Participant.Year.ToString());

      writeElement("Nation", participant.Nation);

      writeElement("Club", participant.Club);

      _writer.WriteEndElement();
    }


    void writeElement(string name, string value)
    {
      _writer.WriteStartElement(name);
      _writer.WriteValue(value ?? "");
      _writer.WriteEndElement();
    }


    static string formatTime(TimeSpan? time)
    {
      return time?.ToString(@"mm\:ss\.ff");
    }


    static string getFISDiscipline(Race race)
    {
      if (race.RaceType == Race.ERaceType.DownHill)
        return "DH";
      if (race.RaceType == Race.ERaceType.GiantSlalom)
        return "GS";
      if (race.RaceType == Race.ERaceType.ParallelSlalom)
        return "PSL";
      if (race.RaceType == Race.ERaceType.Slalom)
        return "SL";
      if (race.RaceType == Race.ERaceType.SuperG)
        return "SG";

      throw new FISExportException("not supported racetype");
    }


    static void mapNotClassifiedStatus(RaceResultItem rri, out string status, out uint failedRun)
    {
      status = "UNKNOWN";
      failedRun = 0;

      foreach (KeyValuePair<uint, RaceResultItem.SubResult> kvp in rri.SubResults.OrderBy(k => k.Key))
      {
        if (kvp.Value.RunResultCode != RunResult.EResultCode.Normal)
        {
          status = mapResultCode(kvp.Value.RunResultCode);
          failedRun = kvp.Key;
          break;
        }
      }
    }

    static string mapResultCode(RunResult.EResultCode resultCode)
    {
      switch (resultCode)
      {
        case RunResult.EResultCode.DIS:
          return "DSQ";
        case RunResult.EResultCode.NaS:
          return "DNS";
        case RunResult.EResultCode.NiZ:
          return "DNF";
        case RunResult.EResultCode.NQ:
          return "DNQ";
      }

      return "UNKNOWN";
    }


    string getSoftware()
    {
      Assembly assembly = Assembly.GetEntryAssembly();
      if (assembly == null)
        assembly = Assembly.GetExecutingAssembly();

      if (assembly != null)
      {
        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        var productName = fvi.ProductName;
        var productVersion = fvi.ProductVersion;

        return string.Format("{0} {1}", productName, productVersion);
      }

      return null;
    }
  }
}
