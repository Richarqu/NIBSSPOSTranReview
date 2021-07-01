using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using Sterling.MSSQL;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace NIBSSPOSTranReview
{
    public class Logics
    {
        public ErrorLog _errorLog;
        private readonly string startDate = ConfigurationManager.AppSettings["startDate"].ToString();
        private readonly string endDate = ConfigurationManager.AppSettings["endDate"];
        private readonly string sterlingBins = ConfigurationManager.AppSettings["bins"];
        private readonly string reviewTable = ConfigurationManager.AppSettings["reviewTable"];
        public DataSet GetDailyPOSData()
        {
            DataSet ds = null;
            try
            {
                string bins = string.Empty;
                //var startDate = Convert.ToInt32(ConfigurationManager.AppSettings["startDate"]);
                //var endDate = Convert.ToInt32(ConfigurationManager.AppSettings["endDate"]);
                var rawBins = sterlingBins.Split(',').ToList();
                foreach(var bin in rawBins)
                {
                    bins += "'" + bin + "',";
                }
                bins = bins.TrimEnd(',');

                new ErrorLog($"Generating NIBSS POS transactions between {startDate} and {endDate}");
                //string sql = @"SELECT Transaction_Date,Merchant_Name,Merchant_Id,Terminal_ID,Amount,BIN,Pan,Acquiring_Bank,Issuing_Bank,Response_Code,System_Trace_No,System_Retrieval_No FROM [postilion_office].[dbo].[NIBSS_POS_RECORDS] where Response_Code = '91' and Transaction_Date between " + startDate + " and " + endDate + "";

                string sql = @"SELECT Transaction_Date,Merchant_Name,Merchant_Id,Terminal_ID,Amount,BIN,Pan,Acquiring_Bank,Issuing_Bank,a.Response_Code,System_Trace_No,System_Retrieval_No FROM [postilion_office].[dbo].[" + reviewTable + "] a inner join [postilion_office].[dbo].[NIBSS_Response_Desc] b on a.Response_Code = b.Response_Code where Transaction_Date between " + startDate + " and " + endDate + " and bin in (" + bins + ") and b.code_group in ('Issuing Bank Error')";

                Connect cn = new Connect("OfficeConn");
                cn.Persist = true;
                cn.SetSQL(sql, 3000);
                ds = cn.Select("recs");
                cn.CloseAll();
            }
            catch (Exception ex)
            {
                new ErrorLog(ex); 
            }
            return ds;
        }

        private DataSet CheckExistence(ColumnList columnList)
        {
            DataSet ds = null;
            try
            {
                new ErrorLog($"Querying Nibss Exception Table for existence of transaction with terminal_id {columnList.Terminal_ID}, rrn {columnList.System_Retrieval_No} and stan {columnList.System_Trace_No}");
                string sql = @"select tran_nr as Postilion_Tran_Nr,rsp_code_rsp as Postilion_Rsp_Code,DateDiff(SECOND,datetime_req,datetime_rsp) as Postilion_Tran_Seconds from NIBSS_POS_TRAN_EXCEPTIONS where terminal_id = '" + columnList.Terminal_ID + "' and system_trace_audit_nr = '" + columnList.System_Trace_No + "' and retrieval_reference_nr = '" + columnList.System_Retrieval_No + "' and pan = '" + columnList.Pan + "' and datetime_req between '" + startDate + "' and '" + endDate + "' and tran_postilion_originated = 0";

                Connect cn = new Connect("OfficeConn");
                cn.Persist = true;
                cn.SetSQL(sql, 3000);
                ds = cn.Select("recs");
                cn.CloseAll();
            }
            catch (Exception ex)
            {
                new ErrorLog($"Exceptiion at method CheckExistence: {ex}.");
            }
            return ds;
        }

        public void CheckPostilionOffice(List<ColumnList> _colList)
        {
            InsertReconRecords _insert = new InsertReconRecords();
            DataSet ds = null;
            foreach (var _columnList in _colList)
            {
                try
                {
                    new ErrorLog($"Querying Postlion Office DB for existence of transaction with terminal_id {_columnList.Terminal_ID}, rrn {_columnList.System_Retrieval_No} and stan {_columnList.System_Trace_No}");
                    string sql = @"select tran_nr as Postilion_Tran_Nr,rsp_code_rsp as Postilion_Rsp_Code,DateDiff(SECOND,datetime_req,datetime_rsp) as Postilion_Tran_Seconds from post_tran a with (nolock) inner join post_tran_cust b with (nolock) on a.post_tran_cust_id = b.post_tran_cust_id where terminal_id = '"+_columnList.Terminal_ID+"' and system_trace_audit_nr = '"+_columnList.System_Trace_No+"' and retrieval_reference_nr = '"+_columnList.System_Retrieval_No+"' and pan = '"+_columnList.Pan+"' and datetime_req between "+startDate+" and "+endDate+" and tran_postilion_originated = 0 and message_type in ('0100','0200')";

                    Connect cn = new Connect("OfficeConn");
                    cn.Persist = true;
                    cn.SetSQL(sql, 3000);
                    ds = cn.Select("recs");
                    cn.CloseAll();

                    bool hasRows = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

                    if (!hasRows)
                    {
                        DataSet checkExist = CheckExistence(_columnList);
                        bool check = checkExist.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                        if (check) { continue; }
                        else
                        {
                            new ErrorLog($"Record with terminal_id {_columnList.Terminal_ID}, stan {_columnList.System_Trace_No}, rrn {_columnList.System_Retrieval_No} does not exist within Sterling Postilion, log record in Exception Table.");

                            _insert.InsertRecords(_columnList, "NIBSS_POS_TRAN_EXCEPTIONS");
                        }
                    }
                    else
                    {
                        //check if the response given by NIBSS is same as what we responded
                        int cnt = ds.Tables[0].Rows.Count;
                        if (hasRows)
                        {
                            string office_rsp = string.Empty;
                            string secTaken = string.Empty;
                            string tranNr = string.Empty;
                            for (int j = 0; j < cnt; j++)
                            {
                                DataRow dr = ds.Tables[0].Rows[j];
                                office_rsp = string.IsNullOrEmpty(dr["Postilion_Rsp_Code"].ToString()) ? "" : dr["Postilion_Rsp_Code"].ToString();
                                secTaken = dr["Postilion_Tran_Seconds"].ToString();
                                tranNr = dr["Postilion_Tran_Nr"].ToString();

                                new ErrorLog($"Record with terminal_id {_columnList.Terminal_ID}, stan {_columnList.System_Trace_No}, rrn {_columnList.System_Retrieval_No} exist in Postilion with response: {office_rsp}");
                                if (_columnList.Response_Code != office_rsp)
                                {
                                    _insert.InsertRspMismatchRecs(_columnList, "NIBSS_POSTILION_FAILED_RECORDS", office_rsp, secTaken, tranNr);
                                }
                            }
                        }
                    }

                    /*
                    else
                    {
                        string filename = $"0620_Reversal_{DateTime.Now:ddMMyyyy}_{DateTime.Now.Hour}hr";
                        string path = ConfigurationManager.AppSettings["filepath"];
                        path += filename + "\\";
                        Generate0620Filecsv(path, filename, _columnList);
                    }
                    */
                }
                catch (Exception ex)
                {
                    _errorLog = new ErrorLog(ex);
                    continue;
                }
            }
        }
        public List<ColumnList> FillDataList(DataSet ds)
        {
            //var startDate = Convert.ToInt32(ConfigurationManager.AppSettings["startDate"]);
            //var endDate = Convert.ToInt32(ConfigurationManager.AppSettings["endDate"]);

            ColumnList _colItems = new ColumnList();
            List<ColumnList> _dataList = new List<ColumnList>();

            try
            {
                bool hasRows = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                if (hasRows)
                {
                    int cnt = ds.Tables[0].Rows.Count;
                    new ErrorLog($"Processing {cnt} records for Failed Nibss POS transactions between {startDate} and {endDate}");
                    //DataSet tRecs = null;
                    int colCnt = ds.Tables[0].Columns.Count;
                    if (cnt > 0)
                    {
                        //string[] mRecs = new string[cols];
                        int i = 0;
                        for (int j = 0; j < cnt; j++)
                        {
                            _colItems = new ColumnList();
                            DataRow dr = ds.Tables[0].Rows[j];
                            i = 0;
                            foreach (DataColumn dc in ds.Tables[0].Columns)
                            {
                                switch (dc.ColumnName)
                                {
                                    case "Transaction_Date":
                                        _colItems.Transaction_Date = dr[i].ToString().Trim();
                                        break;
                                    case "Merchant_Name":
                                        _colItems.Merchant_Name = dr[i].ToString().Trim();
                                        break;
                                    case "Merchant_Id":
                                        _colItems.Merchant_Id = dr[i].ToString().Trim();
                                        break;
                                    case "Terminal_ID":
                                        _colItems.Terminal_ID = dr[i].ToString().Trim();
                                        break;
                                    case "Amount":
                                        _colItems.Amount = dr[i].ToString().Trim();
                                        break;
                                    case "BIN":
                                        _colItems.BIN = dr[i].ToString().Trim();
                                        break;
                                    case "Pan":
                                        _colItems.Pan = dr[i].ToString().Trim();
                                        break;
                                    case "Acquiring_Bank":
                                        _colItems.Acquiring_Bank = dr[i].ToString().Trim();
                                        break;
                                    case "Issuing_Bank":
                                        _colItems.Issuing_Bank = dr[i].ToString().Trim();
                                        break;
                                    case "Response_Code":
                                        _colItems.Response_Code = dr[i].ToString().Trim();
                                        break;
                                    case "System_Trace_No":
                                        _colItems.System_Trace_No = dr[i].ToString().Trim();
                                        break;
                                    case "System_Retrieval_No":
                                        _colItems.System_Retrieval_No = dr[i].ToString().Trim();
                                        break;
                                }
                                i++;
                            }
                            _dataList.Add(_colItems);
                        }
                    }
                }
                return _dataList;
            }
            catch (Exception ex)
            {
                _errorLog = new ErrorLog(ex);
                return _dataList = null;
            }
        }
        private bool CheckPostcard(string acct, string pan, string seq)
        {
            bool status = false;
            try
            {
                string rightPan = pan.Substring(pan.Length - 4, 4);
                DataSet ds = null;
                string query = @"select hold_rsp_code from pc_cards with (nolock) where pan in (select pan from pc_card_accounts with (nolock) where account_id = '" + acct + "' and right(pan,4) = '" + rightPan + "') and seq_nr = " + seq + "";

                PostcardConn conn = new PostcardConn(query);
                ds = conn.query("recs");
                conn.close();

                bool hasRows = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                int cnt = ds.Tables[0].Rows.Count;
                if (hasRows)
                {
                    for (int j = 0; j < cnt; j++)
                    {
                        DataRow dr = ds.Tables[0].Rows[j];
                        status = string.IsNullOrEmpty(dr["hold_rsp_code"].ToString()) ? false : true;
                    }
                }
            }
            catch (Exception ex)
            {
                new ErrorLog("Exception at method CheckPostcard");
                new ErrorLog(ex);
            }
            return status;
        }
        public void GenAttachment01()
        {
            try
            {
                string pth = ConfigurationManager.AppSettings["GenPath01"];
                string filename = "01_TranFailures" + DateTime.Now.ToString("yyyyMMdd") + DateTime.Now.Hour;
                var queryMinStart = ConfigurationManager.AppSettings["queryMinStart_hour"];
                var queryMinEnd = ConfigurationManager.AppSettings["queryMinEnd"];
                DataSet ds = null;
                string query = @"select card_seq_nr as Seq_Nr,pan as PAN,rsp_code_rsp AS Tran_Rsp_Code,CONVERT(VARCHAR,datetime_req,120) AS Date_Time_Req,case when CONVERT(VARCHAR,datetime_rsp,120) IS NULL then '-' else CONVERT(VARCHAR,datetime_rsp,120) end AS Date_Time_Rsp,tran_nr AS Tran_Nr,case when from_account_id IS NULL then '-' else from_account_id end AS From_Account,tran_type AS Tran_Type,source_node_name AS Source_Node,case when sink_node_name IS NULL then '-' else sink_node_name end AS Sink_Node,tran_type+system_trace_audit_nr+retrieval_reference_nr+terminal_id AS Unique_ID,case when DATEDIFF(second,datetime_req,datetime_rsp) IS NULL then NULL else DATEDIFF(second,datetime_req,datetime_rsp) end AS Sec_Time_Taken from post_tran a with(nolock) inner join post_tran_cust b with(nolock) on a.post_tran_cust_id = b.post_tran_cust_id where rsp_code_rsp in ('01') and datetime_req BETWEEN " + queryMinStart + " and " + queryMinEnd + " and sink_node_name in ('SBPMASN24snk', 'SBPMAST24snk', 'SBPMASDOLsnk', 'SBPMASGBPsnk', 'SBPT24Trfsnk', 'SBPT24snk', 'SBPVIST24snk', 'SBPVISDOLsnk') and tran_postilion_originated = 0";

                FEPConn conn = new FEPConn(query);
                ds = conn.query("recs");
                conn.close();

                bool hasRows = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                int cnt = ds.Tables[0].Rows.Count;
                if (hasRows)
                {
                    //first of all check postcard if the card has restriction
                    //else check T24 if it failed with any restriction
                    for (int j = 0; j < cnt; j++)
                    {
                        string uniqueId = string.Empty;
                        string seq_nr = string.Empty;
                        string from_acct = string.Empty;
                        string pan = string.Empty;
                        string FT = string.Empty;
                        DataRow dr = ds.Tables[0].Rows[j];
                        from_acct = dr["From_Account"].ToString();
                        seq_nr = dr["Seq_Nr"].ToString();
                        pan = dr["PAN"].ToString();
                        uniqueId = dr["Unique_ID"].ToString();
                        var checkPostcard = CheckPostcard(from_acct, pan, seq_nr);
                        if (checkPostcard)
                        {
                            continue;
                            //search T24 for FT
                            // FT = "Card " + pan + " blocked on Postcard with hold response 01";
                        }
                        else
                        {
                            t24WebService.banksSoapClient soapClient = new t24WebService.banksSoapClient();
                            var soapResult = soapClient.GetATMTrxnDetailByID(uniqueId);
                            bool hasData = soapResult.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                            if (hasData)
                            {
                                DataRow tdr = soapResult.Tables[0].Rows[0];
                                string FTRef = tdr["errormsg"].ToString().Trim().ToLower();
                                FT = string.IsNullOrEmpty(FTRef) ? string.Empty : FTRef;
                            }
                        }
                        RowData data = new RowData()
                        {
                            Date_Time_Req = dr["Date_Time_Req"].ToString(),
                            Date_Time_Rsp = dr["Date_Time_Rsp"].ToString(),
                            From_Account = dr["From_Account"].ToString(),
                            FT_Ref = FT.ToUpper(),
                            Sec_Time_Taken = dr["Sec_Time_Taken"].ToString(),
                            Sink_Node = dr["Sink_Node"].ToString(),
                            Source_Node = dr["Source_Node"].ToString(),
                            Tran_Nr = dr["Tran_Nr"].ToString(),
                            Tran_Rsp_Code = dr["Tran_Rsp_Code"].ToString(),
                            Tran_Type = dr["Tran_Type"].ToString(),
                            Unique_ID = dr["Unique_ID"].ToString()
                        };
                        GenGenericDelimCsv(pth, filename, data, ",");
                    }
                }
            }
            catch (Exception ex)
            {
                new ErrorLog("Exception at method GenAttachment01");
                new ErrorLog(ex);
            }
        }
        private void GenGenericDelimCsv(string folderpth, string filename, RowData input, string delim)
        {
            Thread.Sleep(10);
            string pth = folderpth + "\\" + filename + ".csv";

            string txt = input.Date_Time_Req + "," + input.Date_Time_Rsp + "," + input.From_Account + "," + input.FT_Ref + "," + input.Sec_Time_Taken + "," + input.Sink_Node + "," + input.Source_Node + "," + input.Tran_Nr + "," + input.Tran_Rsp_Code + "," + input.Tran_Type + "," + input.Unique_ID;

            if (!File.Exists(pth))
            {
                using (StreamWriter sw = File.CreateText(pth))
                {
                    sw.WriteLine("Date_Time_Req,Date_Time_Rsp,From_Account,Reference,Sec_Time_Taken,Sink_Node,Source_Node,Tran_Nr,Tran_Rsp_Code,Tran_Type,Unique_ID");
                    sw.WriteLine(txt);
                    sw.Close();
                    sw.Dispose();
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(pth))
                {
                    sw.WriteLine(txt);
                    sw.Close();
                    sw.Dispose();
                }
            }
        }
        /*
        public void Generate0620Filecsv(string folderpth, string filename, ColumnList input)
        {
            Thread.Sleep(10);

            //Set the location to drop the files
            string pth = folderpth + "\\" + filename + ".csv";
            //set each customer's record per line
            string txt = input.Transaction_Date + "," + input.Merchant_Name + "," + input.Merchant_Id + "," + input.Terminal_ID + "," + input.Amount + "," + input.BIN + "," + input.Pan + "," + input.Acquiring_Bank + "," + input.Issuing_Bank +
                 "," + input.Response_Code + "," + input.System_Trace_No + "," + input.System_Retrieval_No;
            try
            {
                if (!File.Exists(pth))
                {
                    using (StreamWriter sw = File.CreateText(pth))
                    {
                        try
                        {
                            sw.WriteLine(txt);
                            sw.Close();
                            sw.Dispose();
                        }
                        catch (Exception ex)
                        {
                            new ErrorLog(ex);
                        }
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(pth))
                        try
                        {
                            sw.WriteLine(txt);
                            sw.Close();
                            sw.Dispose();
                        }
                        catch (Exception ex)
                        {
                            new ErrorLog(ex);
                        }
                }
            }
            catch (Exception ex)
            {
                new ErrorLog(ex);
            }


        }
        */
    }
    public class RowData
    {
        public string Tran_Rsp_Code { get; set; }
        public string Date_Time_Req { get; set; }
        public string Date_Time_Rsp { get; set; }
        public string Tran_Nr { get; set; }
        public string From_Account { get; set; }
        public string Tran_Type { get; set; }
        public string Source_Node { get; set; }
        public string Sink_Node { get; set; }
        public string Unique_ID { get; set; }
        public string Sec_Time_Taken { get; set; }
        public string FT_Ref { get; set; }
    }
}
