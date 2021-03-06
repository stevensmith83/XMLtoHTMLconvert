﻿using System;
using System.Configuration;
using System.Text;
using System.Globalization;
using System.Data;
using System.Data.OleDb;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace XLStoHTMLconvert
{
    public static class Helper
    {
        public static Dictionary<string, string> correction;
        public static List<string> headerList;

        public static void InitDictionaryAndList()
        {
            correction = new Dictionary<string, string>();
            string input = ConfigurationManager.AppSettings["Correction"];

            if (input.Trim().Length > 0)
            {
                correction = input.TrimEnd(';').Split(';').ToDictionary(item => item.Split('=')[0], item => item.Split('=')[1]);
            }

            headerList = new List<String>();
            headerList = ConfigurationManager.AppSettings["Header"].Split(';').ToList();
        }

        public static void SaveToDictionary(string key, string value)
        {
            if (correction.ContainsKey(key))
            {
                correction[key] = value;
            }
            else
            {
                correction.Add(key, value);
            }
            
            SaveDictionaryToConfig();
        }

        public static void SaveDictionaryToConfig()
        {
            string configString = string.Join(";", correction.Select(x => x.Key + "=" + x.Value));
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            config.AppSettings.Settings.Remove("Correction");
            config.AppSettings.Settings.Add("Correction", configString);
            config.Save(ConfigurationSaveMode.Minimal);
        }

        public static DateTime FirstDateOfWeek(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var weekNum = weekOfYear;

            if (firstWeek <= 1)
            {
                weekNum -= 1;
            }

            var result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3);
        }

        public static DataTable ImportXLS(string fileName)
        {
            string sheetName = ConfigurationManager.AppSettings["SheetName"];
            string connectionString = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + fileName + ";Extended Properties='Excel 8.0;HDR=NO;';";

            OleDbConnection connection = new OleDbConnection(connectionString);
            OleDbCommand command = new OleDbCommand("Select * From [" + sheetName + "$]", connection);
            connection.Open();

            OleDbDataAdapter adapter = new OleDbDataAdapter(command);
            DataTable data = new DataTable();
            adapter.Fill(data);
            return data;
        }

        private static string GetProvider()
        {
            var reader = OleDbEnumerator.GetRootEnumerator();
            var provider = string.Empty;

            while (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i) == "SOURCES_NAME" && reader.GetValue(0).ToString().Contains("Microsoft.ACE.OLEDB"))
                    {
                        provider = reader.GetValue(i).ToString();
                        break;
                    }
                }                
            }

            reader.Close();
            return provider;
        }

        public static string Capitalize(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                return char.ToUpper(input[0]) + input.Substring(1).ToLower();
            }

            return input;            
        }

        public static string FormatFirstCell(DateTime day)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(day.ToString("dddd", new CultureInfo("hu-HU")).ToUpper()).Append(Environment.NewLine);
            builder.Append(day.ToString("MMMM", new CultureInfo("hu-HU"))).Append(" ");
            builder.Append(day.ToString("dd", new CultureInfo("hu-HU"))).Append(".");

            return builder.ToString();
        }

        public static string FormatCellData(string data)
        {
            data = Capitalize(data.Trim());
            data = Regex.Replace(data, "[ ]{2,}", " ");
            data = Regex.Replace(data, @"\,(?! |$)", ", ");
            data = correction.ContainsKey(data) ? correction[data] : data;
            return data;
        }

        public static string Format7thCell(string data, string firstCell, string secondCell)
        {
            data = data.Replace("L1,", firstCell + ";").Replace("L2,", secondCell + ";");
            return string.IsNullOrEmpty(data) ? data : data.Substring(0, data.IndexOf(";")) + Environment.NewLine + Capitalize(data.Substring(data.IndexOf(";") + 2));
        }              

        public static string Format8thCell(string data)
        {
            return data.Replace("e:", Environment.NewLine + "E: ").Replace("szh:", Environment.NewLine + "Szh: "); ;
        }

        public static StringBuilder ConvertTableToBootstrapTable(DataGridView dataGridView)
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine("<div class='table-responsive'>");
            html.AppendLine("\t<table class='table table-hover'>");
            html.Append(CreateHeader(dataGridView[0, 0].Value.ToString()));
            html.Append(CreateRows(dataGridView));
            html.AppendLine("\t</table>");
            html.AppendLine("</div>");
            return html;
        }

        private static string CreateHeader(string firstCell)
        {
            StringBuilder header = new StringBuilder();
            header.AppendLine("\t\t<thead>");
            header.AppendLine("\t\t\t<tr>");
            header.Append("\t\t\t\t<th style='text-align: center; vertical-align: middle;'>").Append(firstCell).AppendLine("</th>");

            foreach (string item in headerList)
            {
                header.Append("\t\t\t\t<th style='text-align: center; vertical-align: middle;'>").Append(item).AppendLine("</th>");
            }

            header.AppendLine("\t\t\t</tr>");
            header.AppendLine("\t\t</thead>");
            return header.ToString();
        }

        private static string CreateRows(DataGridView dataGridView)
        {
            StringBuilder rows = new StringBuilder();
            rows.AppendLine("\t\t<tbody>");            

            for (int row = 2; row < dataGridView.Rows.Count; row++)
            {
                rows.AppendLine("\t\t\t<tr>");
                Boolean firstCell = true;

                foreach (DataGridViewCell cell in dataGridView.Rows[row].Cells)
                {
                    string data = cell.Value.ToString();
                    data = data.Replace(System.Environment.NewLine, "<br><em>");

                    if (firstCell)
                    {
                        rows.Append("\t\t\t\t<th style='text-align: center; vertical-align: middle;'>").Append(data).AppendLine("</em></th>");
                        firstCell = false;
                    }
                    else
                    {
                        rows.Append("\t\t\t\t<td style='text-align: center; vertical-align: middle;'>").Append(data).AppendLine("</em></td>");
                    }
                }

                rows.AppendLine("\t\t\t</tr>");
            }
            
            rows.AppendLine("\t\t</tbody>");
            return rows.ToString();
        }
    }
}
