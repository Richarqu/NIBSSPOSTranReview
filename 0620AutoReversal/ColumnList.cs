using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NIBSSPOSTranReview
{
    public class ColumnList
    {
        public string Transaction_Date { get; set; }
        public string Merchant_Name { get; set; }
        public string Merchant_Id { get; set; }
        public string Terminal_ID { get; set; }
        public string Amount { get; set; }
        public string BIN { get; set; }
        public string Pan { get; set; }
        public string Acquiring_Bank { get; set; }
        public string Issuing_Bank { get; set; }
        public string Response_Code { get; set; }
        public string System_Trace_No { get; set; }
        public string System_Retrieval_No { get; set; }
        //public string Destination_Institution { get; set; } 
    }
}
