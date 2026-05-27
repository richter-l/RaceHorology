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

namespace RaceHorologyLibTest
{
  [TestClass]
  public class MicrogateV2Test
  {
    /// <summary>
    /// Builds a static-response line (line type 18) in the extended-protocol layout used by Microgate V2.
    /// </summary>
    private static string BuildStaticResponseLine(uint startNumber, string logicalChannel, char flag, string time)
    {
      char[] line = new char[40];
      for (int i = 0; i < line.Length; i++)
        line[i] = ' ';

      line[0] = (char)18; // static response

      string sn = startNumber.ToString("D5");
      for (int i = 0; i < 5; i++) line[12 + i] = sn[i];

      string group = "000";
      for (int i = 0; i < 3; i++) line[17 + i] = group[i];

      for (int i = 0; i < 3; i++) line[26 + i] = logicalChannel[i];

      line[29] = flag;

      for (int i = 0; i < 10; i++) line[30 + i] = time[i];

      return new string(line);
    }


    [TestMethod]
    public void BuildImportRequest_EncodesRunAsThreeDigits()
    {
      // \x11 (DC1) + request body, run zero-padded to 3 digits, terminated with \r
      Assert.AreEqual("R 000000000*251001000S\r", MicrogateV2TimeMeasurementBase.BuildImportRequest(1));
      Assert.AreEqual("R 000000000*251002000S\r", MicrogateV2TimeMeasurementBase.BuildImportRequest(2));
      Assert.AreEqual("R 000000000*251123000S\r", MicrogateV2TimeMeasurementBase.BuildImportRequest(123));
    }


    [TestMethod]
    public void SupportedImportTimeFlags_AreRemoteDownloadAndStartFinish()
    {
      var device = new MicrogateV2TimeMeasurement(null, 9600, null);
      Assert.AreEqual(EImportTimeFlags.RemoteDownload | EImportTimeFlags.StartFinishTime, device.SupportedImportTimeFlags());
    }


    [TestMethod]
    public void StaticResponse_StartChannel_ParsedAsStartTimestamp()
    {
      var parser = new MicrogateV2LineParser();
      // Logical channel 000 = start
      parser.Parse(BuildStaticResponseLine(7, "000", '0', "0812345678"));

      Assert.AreEqual(MicrogateV2LineParser.ELineType.StaticResponseLine, parser.LineType);
      Assert.IsNotNull(parser.TimingData);

      var entry = MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData);
      Assert.IsNotNull(entry);
      Assert.AreEqual(7U, entry.StartNumber);
      Assert.AreEqual(new TimeSpan(0, 8, 12, 34, 0).Add(TimeSpan.FromTicks(5678 * (TimeSpan.TicksPerSecond / 10000))), entry.StartTime);
      Assert.IsNull(entry.FinishTime);
      Assert.IsNull(entry.RunTime);
    }


    [TestMethod]
    public void StaticResponse_FinishChannel_ParsedAsFinishTimestamp()
    {
      var parser = new MicrogateV2LineParser();
      // Logical channel 255 = finish
      parser.Parse(BuildStaticResponseLine(7, "255", '0', "0812345678"));

      Assert.AreEqual(MicrogateV2LineParser.ELineType.StaticResponseLine, parser.LineType);

      var entry = MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData);
      Assert.IsNotNull(entry);
      Assert.AreEqual(7U, entry.StartNumber);
      Assert.IsNull(entry.StartTime);
      Assert.AreEqual(new TimeSpan(0, 8, 12, 34, 0).Add(TimeSpan.FromTicks(5678 * (TimeSpan.TicksPerSecond / 10000))), entry.FinishTime);
      Assert.IsNull(entry.RunTime);
    }


    [TestMethod]
    public void TransferToImportTimeEntry_IgnoresUnknownChannelAndUnknownFlag()
    {
      var parser = new MicrogateV2LineParser();

      // Unknown logical channel (e.g. a lap split) on a valid time -> no entry
      parser.Parse(BuildStaticResponseLine(7, "001", '0', "0812345678"));
      Assert.IsNull(MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData));

      // Unknown flag -> no entry
      parser.Parse(BuildStaticResponseLine(7, "000", 'x', "0812345678"));
      Assert.IsNull(MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData));
    }


    [TestMethod]
    public void StaticResponse_ResultCodeFlags_MappedToResultCode()
    {
      var parser = new MicrogateV2LineParser();

      // 'P' = Did not start -> NaS, no times. The line still carries a (start) timecode which is ignored.
      parser.Parse(BuildStaticResponseLine(7, "000", 'P', "0812345678"));
      var dns = MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData);
      Assert.IsNotNull(dns);
      Assert.AreEqual(7U, dns.StartNumber);
      Assert.AreEqual(RunResult.EResultCode.NaS, dns.ResultCode);
      Assert.IsNull(dns.StartTime);
      Assert.IsNull(dns.FinishTime);
      Assert.IsNull(dns.RunTime);

      // 'A' = disqualified -> DIS
      parser.Parse(BuildStaticResponseLine(8, "255", 'A', "0812345678"));
      var dis = MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData);
      Assert.AreEqual(RunResult.EResultCode.DIS, dis.ResultCode);

      // 'Q' = not qualified -> NQ
      parser.Parse(BuildStaticResponseLine(9, "255", 'Q', "0812345678"));
      var nq = MicrogateV2TimeMeasurementBase.TransferToImportTimeEntry(parser.TimingData);
      Assert.AreEqual(RunResult.EResultCode.NQ, nq.ResultCode);
    }
  }
}
