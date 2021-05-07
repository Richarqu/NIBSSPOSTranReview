using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NIBSSPOSTranReview
{
    class Program
    {
        static void Main(string[] args)
        {
            Logics _logics = new Logics();
            //get POS Data within the hour check
            var _getFailedPosTran = _logics.GetDailyPOSData();
            //populate data into a list in preparation for CamGuard validation
            var _fillDataList = _logics.FillDataList(_getFailedPosTran);
            //validate each record in the list against Office and take decision
            _logics.CheckPostilionOffice(_fillDataList);
            //once record validation is complete, generate a .csv of records not found on postilion to be shared with Interswitch.
        }
    }
}
