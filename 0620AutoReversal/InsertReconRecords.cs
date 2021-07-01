//using reports;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NIBSSPOSTranReview
{
    public class InsertReconRecords
    {
        public void InsertRecords(ColumnList input, string tableName)
        {
            Thread.Sleep(10);
            try
            {
                if (input != null) 
                {
                    var query = $"Insert into [dbo].[{tableName}] values (@Transaction_Date,@Merchant_Name,@Merchant_Id,@Terminal_ID,@Amount,@BIN,@Pan,@Acquiring_Bank,@Issuing_Bank,@Response_Code,@System_Trace_No,@System_Retrieval_No);";

                    var _configuration = ConfigurationManager.AppSettings["OfficeConn"];

                    using (SqlConnection connect = new SqlConnection(_configuration))
                    {
                        using (SqlCommand cmd = new SqlCommand(query, connect))
                        {
                            if (connect.State != ConnectionState.Open)
                            {
                                connect.Open();
                            }
                            cmd.CommandType = System.Data.CommandType.Text;
                            cmd.Parameters.AddWithValue("@Transaction_Date", input.Transaction_Date);
                            cmd.Parameters.AddWithValue("@Merchant_Name", input.Merchant_Name);
                            cmd.Parameters.AddWithValue("@Merchant_Id", input.Merchant_Id);
                            cmd.Parameters.AddWithValue("@Terminal_ID", input.Terminal_ID);
                            cmd.Parameters.AddWithValue("@Amount", input.Amount);
                            cmd.Parameters.AddWithValue("@BIN", input.BIN);
                            cmd.Parameters.AddWithValue("@Pan", input.Pan);
                            cmd.Parameters.AddWithValue("@Acquiring_Bank", input.Acquiring_Bank);
                            cmd.Parameters.AddWithValue("@Issuing_Bank", input.Issuing_Bank);
                            cmd.Parameters.AddWithValue("@Response_Code", input.Response_Code);
                            cmd.Parameters.AddWithValue("@System_Trace_No", input.System_Trace_No);
                            cmd.Parameters.AddWithValue("@System_Retrieval_No", input.System_Retrieval_No);
                            int i = cmd.ExecuteNonQuery();
                            connect.Dispose();
                            connect.Close();
                            if (i > 0) { new ErrorLog($"Record with terminal_id {input.Terminal_ID}, stan {input.System_Trace_No}, rrn {input.System_Retrieval_No} inserted successful."); } else {
                                new ErrorLog($"Record with terminal_id {input.Terminal_ID}, stan {input.System_Trace_No}, rrn {input.System_Retrieval_No} could not be inserted.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {

            }
        }

        public void InsertRspMismatchRecs(ColumnList input, string tableName, string officeResp, string timeTaken, string tranNr)
        {
            Thread.Sleep(10);
            try
            {
                if (input != null)
                {
                    var query = $"Insert into [dbo].[{tableName}] values (@Tran_Nr,@Transaction_Date,@Merchant_Name,@Merchant_Id,@Terminal_ID,@Amount,@BIN,@Pan,@Acquiring_Bank,@Issuing_Bank,@Response_Code,@Postilion_Response,@System_Trace_No,@System_Retrieval_No,@Time_Taken);";

                    var _configuration = ConfigurationManager.AppSettings["OfficeConn"];

                    using (SqlConnection connect = new SqlConnection(_configuration))
                    {
                        using (SqlCommand cmd = new SqlCommand(query, connect))
                        {
                            if (connect.State != ConnectionState.Open)
                            {
                                connect.Open();
                            }
                            cmd.CommandType = System.Data.CommandType.Text;
                            cmd.Parameters.AddWithValue("@Tran_Nr", tranNr);
                            cmd.Parameters.AddWithValue("@Transaction_Date", input.Transaction_Date);
                            cmd.Parameters.AddWithValue("@Merchant_Name", input.Merchant_Name);
                            cmd.Parameters.AddWithValue("@Merchant_Id", input.Merchant_Id);
                            cmd.Parameters.AddWithValue("@Terminal_ID", input.Terminal_ID);
                            cmd.Parameters.AddWithValue("@Amount", input.Amount);
                            cmd.Parameters.AddWithValue("@BIN", input.BIN);
                            cmd.Parameters.AddWithValue("@Pan", input.Pan);
                            cmd.Parameters.AddWithValue("@Acquiring_Bank", input.Acquiring_Bank);
                            cmd.Parameters.AddWithValue("@Issuing_Bank", input.Issuing_Bank);
                            cmd.Parameters.AddWithValue("@Response_Code", input.Response_Code);
                            cmd.Parameters.AddWithValue("@Postilion_Response", officeResp);
                            cmd.Parameters.AddWithValue("@System_Trace_No", input.System_Trace_No);
                            cmd.Parameters.AddWithValue("@System_Retrieval_No", input.System_Retrieval_No);
                            cmd.Parameters.AddWithValue("@Time_Taken", timeTaken);
                            int i = cmd.ExecuteNonQuery();
                            connect.Dispose();
                            connect.Close();
                            if (i > 0) { new ErrorLog($"Record with terminal_id {input.Terminal_ID}, stan {input.System_Trace_No}, rrn {input.System_Retrieval_No} inserted successful."); }
                            else
                            {
                                new ErrorLog($"Record with terminal_id {input.Terminal_ID}, stan {input.System_Trace_No}, rrn {input.System_Retrieval_No} could not be inserted.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {

            }
        }
    }
}
