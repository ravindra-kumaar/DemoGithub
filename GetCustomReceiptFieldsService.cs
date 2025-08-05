namespace rkg
{
    namespace Commerce.Runtime.ReceiptsSample
    {
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Text;
        using System.Threading.Tasks;
        using Microsoft.Dynamics.Commerce.Runtime;
        using Microsoft.Dynamics.Commerce.Runtime.Data;
        using Microsoft.Dynamics.Commerce.Runtime.Data.Types;
        using Microsoft.Dynamics.Commerce.Runtime.DataModel;
        using Microsoft.Dynamics.Commerce.Runtime.DataServices.Messages;
        using Microsoft.Dynamics.Commerce.Runtime.Messages;
        using Microsoft.Dynamics.Commerce.Runtime.Services.Messages;
        using SixLabors.ImageSharp;
        using SixLabors.ImageSharp.Processing;
        using Acx.CRT.Extensions.BestSeller;
        using Microsoft.Dynamics.Commerce.Runtime.DataAccess.SqlServer;
        using Microsoft.Dynamics.Commerce.Runtime.RealtimeServices.Messages;
        using System.Collections.ObjectModel;

        public static class GlobalVariablesCounter
        {
            public static int GlobalSRNo = 1;
            public static int GlobalCreditNoteTotal = 0;
            public static int GlobalCreditNoteCount = 0;
        }

        /// <summary>
        /// The extended service to get custom receipt field.
        /// </summary>
        /// <remarks>
        /// To print custom receipt fields on a receipt, one must handle <see cref="GetSalesTransactionCustomReceiptFieldServiceRequest"/>
        /// and <see cref="GetCustomReceiptFieldServiceResponse"/>. Here are several points about how to do this.
        /// 1. CommerceRuntime calls this request if and only if it needs to print values for some fields that are not supported by default. So make
        ///    sure your custom receipt fields have different names than existing ones. Adding a prefix in front of custom filed names would
        ///    be a good idea. The value of custom filed name should match the value you defined in AX, on Custom Filed page.
        /// 2. User should handle content-related formatting. This means that if you want to print "$ 10.00" instead of "10" on the receipt, 
        ///    you must generate "$ 10.00" by yourself. You can call <see cref="GetFormattedCurrencyServiceRequest"/> to do this. There are also some
        ///    other requests designed to format other types of values such as numbers and date time. Note, the user DO NOT need to worry about alignment,
        ///    the CommerceRuntime will take care of that.
        /// 3. If any exception happened when getting the value of custom receipt fields, CommerceRuntime will print empty value on the receipt and the
        ///    exceptions will be logged.
        /// 4. So far, only sales-transaction-based custom receipts are supported. This means you can do customization for receipts when checking out
        ///    a normal sales transaction or creating/picking up a customer order.
        /// </remarks>
        public class GetCustomReceiptFieldsService : IRequestHandlerAsync
        {
            public static string QRGstin = string.Empty;
            public static string QRUPI = string.Empty;
            public static string QRBankDetails = string.Empty;
            public static string QRTaxDetails = string.Empty;
            public static string QRInvoiceDetails = string.Empty;
            public static string QRInvAmount = string.Empty;
            public static string QRPayments = string.Empty;
            public IEnumerable<Type> SupportedRequestTypes
            {
                get
                {
                    return new[]
                    {
                        typeof(GetSalesTransactionCustomReceiptFieldServiceRequest),
                    };
                }
            }
            string response = "";
            string GVcode = "";
            public async Task<Response> Execute(Request request)
            {
                if (request == null)
                {
                    throw new ArgumentNullException("request");
                }

                Type requestedType = request.GetType();

                if (requestedType == typeof(GetSalesTransactionCustomReceiptFieldServiceRequest))
                {

                    return await this.GetCustomReceiptFieldForSalesTransactionReceiptsAsync((GetSalesTransactionCustomReceiptFieldServiceRequest)request).ConfigureAwait(false);
                }
                throw new NotSupportedException(string.Format("Request '{0}' is not supported.", request.GetType()));
            }
            //private async Task<Response> GetCustomReceiptFieldReceiptsAsync(GetReceiptServiceRequest request)
            //{
            //    SalesOrder salesOrder = request.SalesOrder;
            //    string Alterationslip = salesOrder.Comment;
            //    string output = await New(Alterationslip, request).ConfigureAwait(false);

            //    // Create a Response object with the generated output as the content
            //    var response = new ConcreteResponse(Microsoft.Dynamics.Commerce.Runtime.Messages.ResponseType.Success, output);

            //    // Return the Response object
            //    return response;
            //}
            //private async Task<Microsoft.Dynamics.Commerce.Runtime.Messages.Response> GetCustomReceiptFieldReceiptsAsync(GetReceiptServiceRequest request)
            //{
            //    // Call the method that generates the output
            //    SalesOrder salesOrder = request.SalesOrder;
            //    string Alterationslip = salesOrder.Comment;
            //    string output = await New(Alterationslip, request).ConfigureAwait(false);
            //    // Create an instance of a concrete subclass using a factory method
            //    var response = ResponseFactory.CreateSuccessResponse(output);

            //    // Return the Response object
            //    return response;
            //}
            private async Task<Response> GetCustomReceiptFieldForSalesTransactionReceiptsAsync(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                string strHSN = string.Empty;
                string strCGSTPerc = string.Empty;
                string strSGSTPerc = string.Empty;
                string strIGSTPerc = string.Empty;
                string strTotalCGSTValue = string.Empty;
                string strTotalSGSTValue = string.Empty;
                string strTotalIGSTValue = string.Empty;
                string strTotalGSTValue = string.Empty;
                string receiptFieldName = request.CustomReceiptField;

                SalesOrder salesOrder = request.SalesOrder;
                SalesLine salesLine = request.SalesLine;
                TenderLine tenderLine = request.TenderLine;

                // Get the store currency.
                string currency = request.RequestContext.GetOrgUnit().Currency;
                string returnValue = null;
                using (SqlServerDatabaseContext databaseContext = new SqlServerDatabaseContext(request.RequestContext))
                {
                    string qryRtpt = @"SELECT PAYMENTAUTHORIZATION FROM AX.RETAILTRANSACTIONPAYMENTTRANS where TRANSACTIONID=@TRANSACTIONID AND TENDERTYPE ='59'  AND STORE=@STORE AND DATAAREAID=@DATAAREAID";
                    ParameterSet para = new ParameterSet();
                    para["@TRANSACTIONID"] = request.SalesOrder.Id;
                    para["STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                    para["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                    DataSet qrrtpt = await databaseContext.ExecuteQueryDataSetAsync(qryRtpt, para).ConfigureAwait(false);
                    if (qrrtpt != null && qrrtpt.Tables[0].Rows.Count > 0)
                    {
                        string Gvcode = string.Empty;
                        Gvcode = qrrtpt.Tables[0].Rows[0]["PAYMENTAUTHORIZATION"].ToString();
                        if (Gvcode == "" || string.IsNullOrEmpty(Gvcode))
                        {
                            await this.syncData((GetSalesTransactionCustomReceiptFieldServiceRequest)request).ConfigureAwait(false);
                        }

                    }
                }
                switch (receiptFieldName)
                {
                    //case "Alteration_Det":
                    //    string Alterationslip = salesOrder.Comment;
                    //    returnValue = await New(Alterationslip, request).ConfigureAwait(false);
                    //    //returnValue = "9528594496"; ///await AllTaxDetailsWithItem(salesLine, request).ConfigureAwait(false);
                    //    break;
                    case "GSTINNO_IN":
                        returnValue = ""; ////// StoreInfo.Rows[0]["STOREGST"].ToString();
                        break;
                    case "TOTAL_DISC":
                        returnValue = await this.GetTotalDiscount(request).ConfigureAwait(false); ////// StoreInfo.Rows[0]["STOREGST"].ToString();
                        break;
                    case "MRP_TOTAL":
                        returnValue = await this.GetTotalMRP(request).ConfigureAwait(false); ////// StoreInfo.Rows[0]["STOREGST"].ToString();
                        break;
                    case "COMPANY_CIN":
                        returnValue = await this.GetCompanyCIN(request).ConfigureAwait(false);
                        break;
                    case "STORE_STATECD":
                        returnValue = await this.GetStoreStateCD(request).ConfigureAwait(false);
                        break;
                    case "STORE_STATENAME":
                        returnValue = await this.GetStoreStateName(request).ConfigureAwait(false);
                        break;
                    case "GROSSAMT":
                        returnValue = await this.GetGrossAmt(request).ConfigureAwait(false);
                        break;
                    case "CUSTOMER_PAN":
                        returnValue = await this.GetCustomerPAN(request).ConfigureAwait(false);
                        break;
                    case "TAXCOMPONENT_IN":
                        List<string> taxComponentList = await this.GetTAXCOMPONENT_IN(request).ConfigureAwait(false);
                        string taxComponentsAsString = string.Join(Environment.NewLine, taxComponentList);
                        returnValue = taxComponentsAsString;
                        break;
                    case "MOP_ACC":
                        returnValue = await this.AllPayment(request).ConfigureAwait(false);
                        break;
                    case "TRANSACTION_TYPE":
                        returnValue = await this.GetReturn(request).ConfigureAwait(false);
                        break;
                    //case "TAXCOMPONENT_IN":
                    //    string concatenatedTAXCOMPONENT = await this.GetTAXCOMPONENT_IN(request);
                    //    string[] taxComponentArray = concatenatedTAXCOMPONENT.Split(';'); // Split the concatenated string
                    //    foreach (string taxComponent in taxComponentArray)
                    //    {
                    //        // Process each taxComponent value here
                    //        // For example, append it to the returnValue
                    //        returnValue += taxComponent + Environment.NewLine;
                    //    }
                    //    break;
                    case "TAX_PERC":
                        List<string> taxPercentageList = await this.GetTAXPERC(request).ConfigureAwait(false);

                        List<string> formattedTaxPercentageStrings = taxPercentageList
                            .Select(taxPercentageString =>
                                decimal.TryParse(taxPercentageString, out decimal taxPercentage)
                                    ? taxPercentage.ToString("0.00")
                                    : "Invalid Percentage")
                            .ToList();

                        string formattedPercentagesAsString = string.Join(Environment.NewLine, formattedTaxPercentageStrings);
                        returnValue = formattedPercentagesAsString;
                        break;

                    case "TAX_BASIS":
                        List<string> taxBasisList = await this.GetTAXBASIS(request).ConfigureAwait(false);
                        List<string> formattedTaxBasissAsStrings = taxBasisList
                           .Select(taxBasissAsString =>
                               decimal.TryParse(taxBasissAsString, out decimal taxBasis)
                                   ? taxBasis.ToString("0.00")
                                   : "Invalid Basis")
                           .ToList();

                        string formattedTaxBasissAsString = string.Join(Environment.NewLine, formattedTaxBasissAsStrings);
                        returnValue = formattedTaxBasissAsString;
                        break;

                    case "TAX_AMOUNT":
                        List<string> taxAmountList = await this.GetTAXAMT(request).ConfigureAwait(false);
                        List<string> formattedTaxAmountsAsStrings = taxAmountList
                           .Select(taxAmountsAsString =>
                               decimal.TryParse(taxAmountsAsString, out decimal taxAmount)
                                   ? taxAmount.ToString("0.00")
                                   : "Invalid Amount")
                           .ToList();

                        string formattedTaxAmountsAsString = string.Join(Environment.NewLine, formattedTaxAmountsAsStrings);
                        returnValue = formattedTaxAmountsAsString;
                        break;

                    //case "SALESGROUP_NAME":

                    //    returnValue = await this.GetFranchiseeHOAdd(request);
                    //    break;
                    //case "PAYMENT_REFNO":

                    //    returnValue = await this.GetFranchiseeHOAdd(request);
                    //    break;
                    case "SACCODE_IN":
                        returnValue = await this.GetSACCODE_IN(request).ConfigureAwait(false);
                        break;
                    //case "DYNAMICQR_CODE":
                    //    returnValue = await this.GetFranchiseeHOAdd(request);
                    //    break;
                    case "CUSTOMER_MOBILE_NO":
                        returnValue = await this.GetCustomerMobileNo(request).ConfigureAwait(false);
                        break;
                    //case "DELIVERY_STATE_NAME":
                    //    returnValue = await this.GetFranchiseeHOAdd(request);
                    //    break;
                    case "FRANCHISEE_HOADD":
                        returnValue = await this.GetFranchiseeHOAdd(request).ConfigureAwait(false);
                        break;
                    case "FRANCHISEE_CIN":
                        returnValue = await this.GetFranchiseeCIN(request).ConfigureAwait(false);
                        break;
                    //case "REFERENCE_NO":
                    //  returnValue = await this.GetPaymentRefNo(requestm
                    //    break;
                    case "COUPON_ID":
                        returnValue = await this.GetCouID(request).ConfigureAwait(false);
                        break;
                    case "COUPON_DISCLAIMER":
                        //returnValue = await this.AllTaxDetailsWithItem(salesLine, request);

                        returnValue = await this.GetCouponDISCLAIMER(request).ConfigureAwait(false);
                        break;
                    case "COUPON_DISCLAIMER1":
                        returnValue = await this.GetCouponDISCLAIMERArray(request, 1).ConfigureAwait(false);
                        break;
                    case "COUPON_DISCLAIMER2":
                        returnValue = await this.GetCouponDISCLAIMERArray(request, 2).ConfigureAwait(false);
                        break;
                    case "COUPON_DISCLAIMER3":
                        returnValue = await this.GetCouponDISCLAIMERArray(request, 3).ConfigureAwait(false);
                        break;
                    case "COUPON_CAPTION":
                        returnValue = await this.GeTnCoupDetail(request).ConfigureAwait(false);
                        break;
                    case "COU_EXP_DATE":
                        returnValue = await this.GetExp(request).ConfigureAwait(false);
                        break;
                    case "COU_VALUE":
                        returnValue = await this.GetDisValue(request).ConfigureAwait(false);
                        break;
                    case "COUPON_ID_CAP":
                        returnValue = await this.GetNCouID(request).ConfigureAwait(false);
                        break;
                    case "COU_EXP_DATE_CAP":
                        returnValue = await this.GetNExp(request).ConfigureAwait(false);
                        break;
                    case "COU_VALUE_CAP":
                        returnValue = await this.GetNDisValue(request).ConfigureAwait(false);
                        break;
                    case "COUPON_HEADER":
                        returnValue = await this.GeTHeader(request).ConfigureAwait(false);
                        break;
                    case "COUPON_FOOTER":
                        returnValue = await this.GetFooter(request).ConfigureAwait(false);
                        break;

                    case "TAXINVOICE_QR":
                        returnValue = await this.GetQRCode(request).ConfigureAwait(false);
                        break;
                    case "STORE_GSTIN":
                        returnValue = await this.GetStoreGSTIN(request).ConfigureAwait(false);
                        break;
                    case "PAYMENT_TYPE":
                        returnValue = await this.GetCardType(request).ConfigureAwait(false);
                        break;
                    case "ITEM_GSTPERC":
                        returnValue = await this.AllTaxDetailsWithItem(salesLine, request).ConfigureAwait(false);
                        break;
                    case "UPI_PAYMENTREF":
                        //returnValue = await this.GetPaymentRefNo(request); 
                        returnValue = await this.GetPaymentRefNo(request, tenderLine).ConfigureAwait(false);
                        break;
                    case "LEGAL_ADD":
                        returnValue = await this.GetEntityName(request).ConfigureAwait(false);
                        break;
                    case "LEGAL_ADD2":
                        returnValue = await this.GetEntityNamet(request).ConfigureAwait(false);
                        break;
                    case "LEGAL_ADD3":
                        returnValue = await this.GetEntityNameadd(request).ConfigureAwait(false);
                        break;
                    case "ENTITY_NAME":
                        returnValue = await this.GetEntity(request).ConfigureAwait(false);
                        break;
                    case "XENO_COUPONCODE":
                        returnValue = await this.XenoCoupon(request).ConfigureAwait(false);
                        break;
                    case "GLOBAL_SALESGROUPNAME":
                        returnValue = await this.GetSalesGroupName(salesLine, request).ConfigureAwait(false);
                        break;
                    case "REDEEM_CN":
                        returnValue = await this.GetRedeemCN(request).ConfigureAwait(false);
                        break;
                    case "NEW_CNISSUE":
                        returnValue = await this.GetIssueCreditNoteNumber(request).ConfigureAwait(false);
                        break;
                    case "CN_ACTUALREDEEMVALUE":
                        returnValue = await this.GetActualRedeemAmount(salesLine, request).ConfigureAwait(false);
                        break;
                    case "CN_VALIDITY":
                        returnValue = await this.GetIssueCreditValidity(request).ConfigureAwait(false);
                        if (returnValue != "")
                        {
                            returnValue = "CN Expiry Date:" + "\t \t \t \t" + returnValue;
                        }
                        break;
                    //Code by Dipanshu 0702205--start
                    case "ER_COUPONCODE":
                        returnValue = await this.ErCoupon(salesLine, request).ConfigureAwait(false);
                        break;
                        //Code by Dipanshu 0702205--end

                }
                return new GetCustomReceiptFieldServiceResponse(returnValue);
            }
            private async Task<string> FormatCurrencyAsync(decimal value, string currencyCode, RequestContext context)
            {
                GetRoundedValueServiceRequest roundingRequest = null;

                string currencySymbol = string.Empty;

                // Get the currency symbol.
                if (!string.IsNullOrWhiteSpace(currencyCode))
                {
                    var getCurrenciesDataRequest = new GetCurrenciesDataRequest(currencyCode, QueryResultSettings.SingleRecord);
                    var currencyResponse = await context.Runtime.ExecuteAsync<EntityDataServiceResponse<Currency>>(getCurrenciesDataRequest, context).ConfigureAwait(false);
                    Currency currency = currencyResponse.PagedEntityCollection.FirstOrDefault();
                    currencySymbol = currency.CurrencySymbol;
                }

                roundingRequest = new GetRoundedValueServiceRequest(value, currencyCode, 0, false);

                var roundedValueResponse = await context.ExecuteAsync<GetRoundedValueServiceResponse>(roundingRequest).ConfigureAwait(false);
                decimal roundedValue = roundedValueResponse.RoundedValue;

                var formattingRequest = new GetFormattedCurrencyServiceRequest(roundedValue, currencySymbol);
                var formattedValueResponse = await context.ExecuteAsync<GetFormattedContentServiceResponse>(formattingRequest).ConfigureAwait(false);
                string formattedValue = formattedValueResponse.FormattedValue;
                return formattedValue;
            }
            private decimal CalculateTaxPercentage(SalesLine salesLine)
            {
                decimal taxAmount = decimal.Zero;
                bool taxIncludedInPrice = false;

                foreach (var taxLine in salesLine.TaxLines)
                {
                    taxAmount += taxLine.Amount;
                    taxIncludedInPrice = taxIncludedInPrice || taxLine.IsIncludedInPrice;
                }

                decimal lineTotal = salesLine.Quantity > decimal.Zero ? salesLine.NetAmountWithNoTax() : decimal.Zero;
                decimal taxBasis = taxIncludedInPrice ? lineTotal - taxAmount : lineTotal;
                decimal taxPercentage = taxBasis > decimal.Zero ? (taxAmount * 100) / taxBasis : decimal.Zero;
                return taxPercentage;
            }
            private async Task<string> FormatNumber(decimal percentage, RequestContext context)
            {
                var request = new GetFormattedNumberServiceRequest(percentage);
                var resultFormat = await context.ExecuteAsync<GetFormattedContentServiceResponse>(request).ConfigureAwait(false);
                string result = resultFormat.FormattedValue;
                return result;
            }
            public async Task<string> AllPayment(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                ThrowIf.Null(request, "request");

                DataSet dsPaymentDetails = new DataSet();
                DataTable dtPaymentDetails = null;

                string query = @"SELECT A.LINENUM,A.TRANSACTIONID,ISNULL(INFOCODEID,'')INFOCODEID,SUBSTRING(ISNULL(B.NAME, ''),0,8) AS MOP, 
                        ISNULL(FORMAT(A.AMOUNTMST, '0.00'), '') AS AMOUNT,ISNULL(C.INFORMATION, '')INFORMATION,
                        CASE WHEN A.PAYMENTAUTHORIZATION!=''  THEN SUBSTRING(ISNULL(A.PAYMENTAUTHORIZATION, ''),0,30) 
                        ELSE SUBSTRING(ISNULL(C.INFORMATION, ''),0,30) END AS REFERENCE, SUBSTRING(ISNULL(A.CARDTYPEID, ''),0,6) AS CARDTYPE,
                        CASE WHEN ISNULL(A.CARDTYPEID, '')!='' THEN SUBSTRING(ISNULL(A.CARDTYPEID, ''),0,10) ELSE SUBSTRING(ISNULL(B.NAME, ''),0,10) END TENDERNAME
                        FROM ax.RETAILTRANSACTIONPAYMENTTRANS A LEFT JOIN AX.RETAILTRANSACTIONINFOCODETRANS C ON 
                        C.TRANSACTIONID = A.TRANSACTIONID AND C.PARENTLINENUM = A.LINENUM  AND C.INFOCODEID = 'Ref No.' And C.INPUTTYPE in('0','9')
                        LEFT JOIN ax.RETAILTENDERTYPETABLE B ON B.TENDERTYPEID = A.TENDERTYPE 
                        WHERE A.TRANSACTIONID = @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID AND A.VOIDSTATUS=0";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsPaymentDetails = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    dtPaymentDetails = dsPaymentDetails.Tables[0];
                }

                StringBuilder resultBuilder = new StringBuilder();

                if (dtPaymentDetails != null && dtPaymentDetails.Rows.Count > 0)
                {
                    foreach (DataRow row in dtPaymentDetails.Rows)
                    {
                        string mopString = row["MOP"].ToString().PadRight(8);
                        //string mopString = row["TENDERNAME"].ToString().PadRight(10);

                        //if (string.IsNullOrEmpty(dtPaymentDetails.Rows[0]["REFERENCE"].ToString()))
                        //{
                        //    referenceString = row["INFORMATION"].ToString().PadRight((30- row["INFORMATION"].ToString().Length);
                        //}
                        //else
                        //{
                        //    referenceString = row["REFERENCE"].ToString().PadRight(30);
                        //}
                        string referenceString = row["REFERENCE"].ToString().PadRight(30);
                        //string referenceString = row["REFERENCE"].ToString().PadRight((35 - row["REFERENCE"].ToString().Length));
                        string cardTypeString = row["CARDTYPE"].ToString().PadRight(6);
                        string amountString = row["AMOUNT"].ToString().PadLeft(13);

                        // Ensure that empty fields are filled with spaces
                        //mopString = string.IsNullOrEmpty(mopString) ? new string(' ', 8) : mopString;
                        mopString = string.IsNullOrEmpty(mopString) ? new string(' ', 8) : mopString;
                        referenceString = string.IsNullOrEmpty(referenceString) ? new string(' ', 30) : referenceString;
                        cardTypeString = string.IsNullOrEmpty(cardTypeString) ? new string(' ', 6) : cardTypeString;
                        amountString = string.IsNullOrEmpty(amountString) ? new string(' ', 13) : amountString;
                        //string formattedLine = $"{mopString}{referenceString}{cardTypeString}{amountString}";
                        //string formattedLine = $"{mopString}{referenceString}{amountString}"+Environment.NewLine;
                        string formattedLine = $"{mopString}{cardTypeString}{amountString}";
                        resultBuilder.AppendLine(formattedLine);
                        resultBuilder.AppendLine(referenceString);
                    }
                }

                return resultBuilder.ToString();
            }
            public async Task<string> GetQRPayments(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                ThrowIf.Null(request, "request");

                DataSet dsPaymentDetails = new DataSet();
                DataTable dtPaymentDetails = null;
                string payment = "";
                string query = @"SELECT  CASE WHEN A.PAYMENTAUTHORIZATION!='' THEN ISNULL(A.PAYMENTAUTHORIZATION, '') 
                    ELSE ISNULL(C.INFORMATION, '') END AS REFERENCE,
                    CASE WHEN CHANGELINE=1 THEN 'Change Back' ELSE ISNULL(B.NAME, '') END +
                    CASE WHEN ISNULL(A.CARDTYPEID, '')!='' THEN ',' + ISNULL(A.CARDTYPEID, '') ELSE '' END TENDERNAME,
                    ROUND(A.AMOUNTTENDERED,2) AMOUNTTENDERED
                    FROM ax.RETAILTRANSACTIONPAYMENTTRANS A LEFT JOIN AX.RETAILTRANSACTIONINFOCODETRANS C ON 
                    C.TRANSACTIONID = A.TRANSACTIONID AND C.PARENTLINENUM = A.LINENUM  AND C.INFOCODEID = 'Ref No.' And C.INPUTTYPE in('0','9')
                    LEFT JOIN ax.RETAILTENDERTYPETABLE B ON B.TENDERTYPEID = A.TENDERTYPE 
                    WHERE A.TRANSACTIONID = @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID AND
                    A.VOIDSTATUS = 0 ";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsPaymentDetails = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    dtPaymentDetails = dsPaymentDetails.Tables[0];
                }

                StringBuilder resultBuilder = new StringBuilder();

                if (dtPaymentDetails != null && dtPaymentDetails.Rows.Count > 0)
                {
                    foreach (DataRow row in dtPaymentDetails.Rows)
                    {
                        string mopString = row["TENDERNAME"].ToString();
                        string referenceString = row["REFERENCE"].ToString();
                        string amountString = row["AMOUNTTENDERED"].ToString();
                        payment += "\n" + mopString + (referenceString.Length > 0 ? "(" + referenceString + ")" : "") + "::" + amountString;
                        //payment += "\n" + mopString + "::" + amountString;

                        //string formattedLine = $"\n{mopString}::{amountString}::{referenceString}";
                        //resultBuilder.AppendLine(formattedLine);
                    }
                }

                return payment;//resultBuilder.ToString();
            }
            public async Task<string> GetCompanyCIN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string CIN = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCIN = new DataSet();
                DataTable dtCIN = null;
                string HSNQuery = @"SELECT COMPANYCIN FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtCIN = dsCIN.Tables[0];
                }
                if (dtCIN != null)
                {
                    if (dtCIN.Rows.Count > 0)
                    {
                        CIN = dtCIN.Rows[0]["COMPANYCIN"].ToString();
                        if (string.IsNullOrEmpty(CIN))
                        {
                            CIN = dtCIN.Rows[0]["COMPANYCIN"].ToString();
                        }

                    }
                }


                return CIN;

            }
            public async Task<string> GetStoreGSTIN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string SGSTIN = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSGSTIN = new DataSet();
                DataTable dtSGSTIN = null;
                string HSNQuery = @"SELECT IFSCCODE, BANKACCOUNT,UPIID, STOREGSTIN FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSGSTIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtSGSTIN = dsSGSTIN.Tables[0];
                }
                if (dtSGSTIN != null)
                {
                    if (dtSGSTIN.Rows.Count > 0)
                    {
                        SGSTIN = dtSGSTIN.Rows[0]["STOREGSTIN"].ToString();
                    }
                }
                return SGSTIN;

            }
            public async Task GetQRStoreInfo(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string SGSTIN = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSGSTIN = new DataSet();
                DataTable dtSGSTIN = null;
                string HSNQuery = @"SELECT IFSCCODE, BANKACCOUNT,UPIID, STOREGSTIN FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSGSTIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtSGSTIN = dsSGSTIN.Tables[0];
                }
                if (dtSGSTIN != null)
                {
                    if (dtSGSTIN.Rows.Count > 0)
                    {
                        QRGstin = dtSGSTIN.Rows[0]["STOREGSTIN"].ToString();
                        QRUPI = dtSGSTIN.Rows[0]["UPIID"].ToString();
                        QRBankDetails = dtSGSTIN.Rows[0]["BANKACCOUNT"].ToString() + ", " + dtSGSTIN.Rows[0]["IFSCCODE"].ToString();
                    }
                }


            }
            public async Task<string> GetBankDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Bank = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsBank = new DataSet();
                DataTable dtBank = null;
                string HSNQuery = @"SELECT IFSCCODE, BANKACCOUNT,UPIID FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsBank = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtBank = dsBank.Tables[0];
                }
                if (dtBank != null)
                {
                    if (dtBank.Rows.Count > 0)
                    {
                        Bank = dtBank.Rows[0]["UPIID"].ToString();
                    }
                    if (!string.IsNullOrEmpty(dtBank.Rows[0]["BANKACCOUNT"].ToString()))
                    {
                        Bank += "\n" + dtBank.Rows[0]["BANKACCOUNT"].ToString();
                    }
                    if (!string.IsNullOrEmpty(dtBank.Rows[0]["IFSCCODE"].ToString()))
                    {
                        Bank += ", " + dtBank.Rows[0]["IFSCCODE"].ToString() + "  ";
                    }
                }
                return Bank;

            }
            public async Task<string> GetStoreStateCD(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string SSCD = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSSCD = new DataSet();
                DataTable dtSSCD = null;
                string HSNQuery = @"SELECT STORESTATECD FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSSCD = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtSSCD = dsSSCD.Tables[0];
                }
                if (dtSSCD != null)
                {
                    if (dtSSCD.Rows.Count > 0)
                    {
                        SSCD = dtSSCD.Rows[0]["STORESTATECD"].ToString();
                        if (string.IsNullOrEmpty(SSCD))
                        {
                            SSCD = dtSSCD.Rows[0]["STORESTATECD"].ToString();
                        }

                    }
                }


                return SSCD;

            }
            public async Task<bool> IsB2CTransactionAsync(RequestContext requestContext, SalesOrder salesOrder)
            {
                //if (IsStoreCustomer(requestContext, salesOrder.CustomerId))
                //{
                //    return true;
                //}
                var customer = await GetCustomerAsync(requestContext, salesOrder.CustomerId).ConfigureAwait(false);
                if (customer.AccountNumber != null)
                {
                    return true;
                }
                var address = customer.GetPrimaryAddress();
                if (address.AddressTypeValue != 0)
                {
                    return true;
                }
                //GetPrimaryAddressTaxInformationDataRequest getPrimaryAddressTaxInformationDataRequest = new GetPrimaryAddressTaxInformationDataRequest(address.LogisticsLocationRecordId);
                //AddressTaxInformationIndia addressTaxInformationIndia = (await requestContext.Runtime.ExecuteAsync<SingleEntityDataServiceResponse<AddressTaxInformationIndia>>(getPrimaryAddressTaxInformationDataRequest, requestContext).ConfigureAwait(false)).Entity;
                //if (addressTaxInformationIndia == null
                //    || addressTaxInformationIndia.GstinRegistrationNumber == null
                //    || string.IsNullOrEmpty(addressTaxInformationIndia.GstinRegistrationNumber.RegistrationNumber))
                //{
                //    return true;
                //}
                return false;
            }
            public async Task<string> GetReturn(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string receiptText = " "; // Default value

                ThrowIf.Null(request, "request");

                DataSet dsCIN = new DataSet();
                DataTable dtCIN = null;

                string HSNQuery = @"SELECT NETAMOUNT FROM AX.RETAILTRANSACTIONSALESTRANS WHERE TRANSACTIONID = @TRANSACTIONID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtCIN = dsCIN.Tables[0];
                }

                if (dtCIN != null && dtCIN.Rows.Count > 0)
                {
                    decimal netAmount = 0; // Default value
                    if (decimal.TryParse(dtCIN.Rows[0]["NETAMOUNT"].ToString(), out netAmount))
                    {
                        // Check if NETAMOUNT is positive
                        if (netAmount > 0)
                        {
                            receiptText = "Credit note";//"Return Receipt";
                        }
                        if (netAmount < 0)
                        {
                            receiptText = "Tax Invoice ";
                        }
                    }
                }

                return receiptText;
            }
            public async Task<string> GetTotalMRP(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string amount = " "; // Default value

                ThrowIf.Null(request, "request");

                DataSet ds = new DataSet();


                string HSNQuery = @"SELECT FORMAT(SUM(-1* PRICE*QTY),'#,0.00') as AMTTOTAL FROM AX.RETAILTRANSACTIONSALESTRANS WHERE TRANSACTIONID = @TRANSACTIONID AND DATAAREAID = @DATAAREAID AND TRANSACTIONSTATUS=0";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    ds = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    if (ds != null)
                    {
                        if (ds.Tables.Count > 0)
                        {
                            if (ds.Tables[0].Rows.Count > 0)
                            {
                                if (!string.IsNullOrWhiteSpace(ds.Tables[0].Rows[0]["AMTTOTAL"].ToString()))
                                    amount = ds.Tables[0].Rows[0]["AMTTOTAL"].ToString();
                            }
                        }
                    }
                }
                return amount;

            }
            public async Task<string> GetTotalDiscount(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string amount = " "; // Default value

                ThrowIf.Null(request, "request");

                DataSet ds = new DataSet();


                string HSNQuery = @"SELECT FORMAT(SUM(DISCAMOUNT),'#,0.00') as AMTTOTAL FROM AX.RETAILTRANSACTIONSALESTRANS WHERE TRANSACTIONID = @TRANSACTIONID AND DATAAREAID = @DATAAREAID AND TRANSACTIONSTATUS=0";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    ds = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    if (ds != null)
                    {
                        if (ds.Tables.Count > 0)
                        {
                            if (ds.Tables[0].Rows.Count > 0)
                            {
                                if (!string.IsNullOrWhiteSpace(ds.Tables[0].Rows[0]["AMTTOTAL"].ToString()))
                                    amount = ds.Tables[0].Rows[0]["AMTTOTAL"].ToString();
                            }
                        }
                    }
                }
                return amount;

            }
            private static async Task<Customer> GetCustomerAsync(RequestContext requestContext, string customerId)
            {
                Customer customer = null;
                if (!string.IsNullOrWhiteSpace(customerId))
                {
                    var getCustomerDataRequest = new GetCustomerDataRequest(customerId);
                    SingleEntityDataServiceResponse<Customer> getCustomerDataResponse = await requestContext.ExecuteAsync<SingleEntityDataServiceResponse<Customer>>(getCustomerDataRequest).ConfigureAwait(false);
                    customer = getCustomerDataResponse.Entity;
                }
                return customer;
            }
            public async Task<string> GetTransactionDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string TransDet = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsTransDet = new DataSet();
                DataTable dtTransDet = null;
                string HSNQuery = @"select TRANSACTIONID,TRANSDATE,TRANSTIME,FORMAT(NETAMOUNT,'0.00') as NETAMOUNT from ax.RETAILTRANSACTIONTABLE where TRANSACTIONID = @TRANSACTIONID and DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTransDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTransDet = dsTransDet.Tables[0];
                }
                if (dtTransDet != null)
                {
                    if (dtTransDet.Rows.Count > 0)
                    {
                        TransDet = dtTransDet.Rows[0]["TRANSACTIONID"].ToString();

                        // Format TRANSDATE as "dd-MM-yyyy"
                        if (!string.IsNullOrEmpty(dtTransDet.Rows[0]["TRANSDATE"].ToString()))
                        {
                            TransDet = dtTransDet.Rows[0]["TRANSACTIONID"].ToString();

                            // Format TRANSDATE as "dd-MM-yyyy"
                            DateTime transDate = (DateTime)dtTransDet.Rows[0]["TRANSDATE"];
                            TransDet += "," + transDate.ToString("dd-MM-yyyy");

                            // Format TRANSTIME as "hh:mm:ss"
                        }
                        if (dtTransDet.Rows.Count > 0 && !string.IsNullOrEmpty(dtTransDet.Rows[0]["TRANSTIME"].ToString()))
                        {
                            int transTimeSeconds = int.Parse(dtTransDet.Rows[0]["TRANSTIME"].ToString());

                            // Calculate hours, minutes, and seconds
                            int hours = transTimeSeconds / 3600;
                            int minutes = (transTimeSeconds % 3600) / 60;
                            int seconds = transTimeSeconds % 60;

                            // Format as "hh:mm:ss"
                            TransDet += "," + $" {hours:D2}:{minutes:D2}:{seconds:D2}";
                        }
                        if (!string.IsNullOrEmpty(dtTransDet.Rows[0]["NETAMOUNT"].ToString()))
                        {
                            decimal netAmount = decimal.Parse(dtTransDet.Rows[0]["NETAMOUNT"].ToString());
                            netAmount *= -1; // Multiply by -1
                            TransDet += "\n" + netAmount.ToString();
                        }



                    }
                }
                return TransDet;
            }
            public async Task<string> GetQRInvoiceDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string TransDet = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsTransDet = new DataSet();
                DataTable dtTransDet = null;
                string HSNQuery = @"SELECT TRANSACTIONID,RECEIPTID,TRANSDATE,TRANSTIME,FORMAT(GROSSAMOUNT,'0.00') as GROSSAMOUNT from ax.RETAILTRANSACTIONTABLE where TRANSACTIONID = @TRANSACTIONID and DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTransDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTransDet = dsTransDet.Tables[0];
                }
                if (dtTransDet != null)
                {
                    if (dtTransDet.Rows.Count > 0)
                    {
                        TransDet = dtTransDet.Rows[0]["TRANSACTIONID"].ToString();
                        DateTime transDate = DateTime.Now;
                        // Format TRANSDATE as "dd-MM-yyyy"
                        if (!string.IsNullOrEmpty(dtTransDet.Rows[0]["TRANSDATE"].ToString()))
                        {
                            TransDet = dtTransDet.Rows[0]["TRANSACTIONID"].ToString();
                            transDate = (DateTime)dtTransDet.Rows[0]["TRANSDATE"];
                            TransDet += ", " + transDate.ToString("dd-MM-yyyy");
                        }
                        if (!string.IsNullOrEmpty(dtTransDet.Rows[0]["TRANSTIME"].ToString()))
                        {
                            int transTimeSeconds = int.Parse(dtTransDet.Rows[0]["TRANSTIME"].ToString());
                            // Calculate hours, minutes, and seconds
                            int hours = transTimeSeconds / 3600;
                            int minutes = (transTimeSeconds % 3600) / 60;
                            int seconds = transTimeSeconds % 60;

                            // Format as "hh:mm:ss"
                            TransDet += ", " + $" {hours:D2}:{minutes:D2}:{seconds:D2}";
                        }
                        TransDet += "\n";
                        if (!string.IsNullOrEmpty(dtTransDet.Rows[0]["GROSSAMOUNT"].ToString()))
                        {
                            decimal netAmount = decimal.Parse(dtTransDet.Rows[0]["GROSSAMOUNT"].ToString());
                            netAmount *= -1; // Multiply by -1
                            TransDet += netAmount.ToString();
                        }
                        QRInvoiceDetails = "\n" + dtTransDet.Rows[0]["RECEIPTID"].ToString() + ", " + transDate.ToString("dd-MM-yyyy");
                    }
                }
                return TransDet;
            }
            public async Task<string> GetInvoiceDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Invoice = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsInvoice = new DataSet();
                DataTable dtInvoice = null;
                string HSNQuery = @"SELECT TRANSDATE,RECEIPTID, * FROM AX.RETAILTRANSACTIONTABLE where TRANSACTIONID = @TRANSACTIONID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsInvoice = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtInvoice = dsInvoice.Tables[0];
                }
                if (dtInvoice != null)
                {
                    if (dtInvoice.Rows.Count > 0)
                    {
                        Invoice = " " + dtInvoice.Rows[0]["RECEIPTID"].ToString();

                        // Format TRANSDATE as "dd-MM-yyyy"
                        if (!string.IsNullOrEmpty(dtInvoice.Rows[0]["TRANSDATE"].ToString()))
                        {
                            DateTime transDate = (DateTime)dtInvoice.Rows[0]["TRANSDATE"];
                            Invoice += ", " + transDate.ToString("dd-MM-yyyy");

                        }

                    }
                }
                return Invoice;
            }
            public async Task<string> GetPayDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string PayDet = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsPayDet = new DataSet();
                DataTable dtPayDet = null;
                string HSNQuery = @"select B.NAME AS MOP,FORMAT(A.AMOUNTMST,'0.00') AS AMOUNT, A.PAYMENTAUTHORIZATION AS REFERENCE from ax.RETAILTRANSACTIONPAYMENTTRANS A inner join  ax.RETAILTENDERTYPETABLE B 
                        on B.TENDERTYPEID = A.TENDERTYPE where A.TRANSACTIONID = @TRANSACTIONID and DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsPayDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtPayDet = dsPayDet.Tables[0];
                }

                if (dtPayDet != null && dtPayDet.Rows.Count > 0)
                {
                    PayDet = dtPayDet.Rows[0]["MOP"].ToString();

                    // Add AMOUNT to PayDet if it's not null
                    if (!string.IsNullOrEmpty(dtPayDet.Rows[0]["AMOUNT"].ToString()))
                    {
                        PayDet += $", {dtPayDet.Rows[0]["AMOUNT"].ToString()}";
                    }

                    // Add REFERENCE to PayDet if it's not empty
                    if (!string.IsNullOrEmpty(dtPayDet.Rows[0]["REFERENCE"].ToString()))
                    {
                        PayDet += $", {dtPayDet.Rows[0]["REFERENCE"]}";
                    }
                }

                return PayDet;
            }
            public async Task<string> GetQRCode(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                var salesOrder = request.SalesOrder;
                string qrCode = string.Empty;
                //bool isB2C = await IsB2CTransactionAsync(request.RequestContext, salesOrder);
                bool isB2C = true;
                if (isB2C)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    await GetQRStoreInfo(request).ConfigureAwait(false);
                    stringBuilder.Append(await GetQRInvoiceDetails(request).ConfigureAwait(false));
                    stringBuilder.Append(await GetQRPayments(request).ConfigureAwait(false));
                    stringBuilder.Append("\n" + QRGstin);
                    stringBuilder.Append("\n" + QRUPI);
                    stringBuilder.Append("\n" + QRBankDetails);
                    stringBuilder.Append(QRInvoiceDetails);
                    stringBuilder.Append(await GetQRTaxDetails(request).ConfigureAwait(false));

                    //string transactionDetail = await GetTransactionDetails(request);
                    //string payDetail = await AllPayment(request);
                    //string storeGSTIN = await GetStoreGSTIN(request);
                    //string bankDetails = await GetBankDetails(request);
                    //string invoice = await GetInvoiceDetails(request);
                    //string taxC = await AllTaxDetails(request);

                    //if (!string.IsNullOrEmpty(transactionDetail))
                    //{
                    //    stringBuilder.Append(transactionDetail);
                    //}
                    //if (!string.IsNullOrEmpty(payDetail))
                    //{
                    //    stringBuilder.Append(payDetail);
                    //}
                    //if (!string.IsNullOrEmpty(storeGSTIN))
                    //{
                    //    stringBuilder.Append(storeGSTIN);
                    //}
                    //if (!string.IsNullOrEmpty(bankDetails))
                    //{
                    //    stringBuilder.Append(bankDetails);
                    //}
                    //if (!string.IsNullOrEmpty(invoice))
                    //{
                    //    stringBuilder.Append(invoice);
                    //}
                    //if (!string.IsNullOrEmpty(taxC))
                    //{
                    //    stringBuilder.Append(taxC);
                    //}
                    var qrCodeRequest = new Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceRequest(stringBuilder.ToString())
                    {
                        Width = 250,
                        Height = 250,
                    };
                    Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceResponse qrCodeDataResponse =
                        await request.RequestContext.ExecuteAsync<Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceResponse>(qrCodeRequest).ConfigureAwait(false);

                    string convertedQRCode = ConvertImagePNGToMonochromaticBMP(qrCodeDataResponse.QRCode);
                    qrCode = $"<I:{convertedQRCode}>";
                }
                GlobalVariablesCounter.GlobalCreditNoteCount = 0;
                GlobalVariablesCounter.GlobalCreditNoteTotal = 0;
                return qrCode;

            }
            //public async Task<string> GetQRCodeOld(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            //{
            //    var salesOrder = request.SalesOrder;
            //    string qrCode = string.Empty;
            //    bool isB2C = await IsB2CTransactionAsync(request.RequestContext, salesOrder);

            //    if (isB2C)
            //    {


            //        string transactionDetail = await GetTransactionDetails(request);
            //        string payDetail = await AllPayment(request).ConfigureAwait(false);
            //        string storeGSTIN = await GetStoreGSTIN(request);
            //        string bankDetails = await GetBankDetails(request);
            //        string invoice = await GetInvoiceDetails(request);
            //        string taxC = await AllTaxDetails(request);
            //        StringBuilder stringBuilder = new StringBuilder();
            //        if (!string.IsNullOrEmpty(transactionDetail))
            //        {
            //            stringBuilder.Append(transactionDetail);
            //        }
            //        if (!string.IsNullOrEmpty(payDetail))
            //        {
            //            stringBuilder.Append(payDetail);
            //        }
            //        if (!string.IsNullOrEmpty(storeGSTIN))
            //        {
            //            stringBuilder.Append(storeGSTIN);
            //        }
            //        if (!string.IsNullOrEmpty(bankDetails))
            //        {
            //            stringBuilder.Append(bankDetails);
            //        }
            //        if (!string.IsNullOrEmpty(invoice))
            //        {
            //            stringBuilder.Append(invoice);
            //        }
            //        if (!string.IsNullOrEmpty(taxC))
            //        {
            //            stringBuilder.Append(taxC);
            //        }
            //        var qrCodeRequest = new Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceRequest(stringBuilder.ToString())
            //        {
            //            Width = 300,
            //            Height = 300
            //        };
            //        Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceResponse qrCodeDataResponse =
            //            await request.RequestContext.ExecuteAsync<Microsoft.Dynamics.Commerce.Runtime.Localization.Services.Messages.EncodeQrCodeServiceResponse>(qrCodeRequest).ConfigureAwait(false);

            //        // string convertedQRCode = ConvertImagePNGToMonochromaticBMP(qrCodeDataResponse.QRCode);
            //        // qrCode = $"<I:{convertedQRCode}>";
            //        qrCode = $"<I:{qrCodeDataResponse.QRCode}>";
            //    }
            //    return qrCode;

            //}
            private static string ConvertImagePNGToMonochromaticBMP(string qrCode)
            {
                MemoryStream imgMemStr = new MemoryStream();

                byte[] imageBytes = Convert.FromBase64String(qrCode);
                SixLabors.ImageSharp.Image img = SixLabors.ImageSharp.Image.Load(imageBytes);
                var bmpEncoder = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();
                bmpEncoder.BitsPerPixel = SixLabors.ImageSharp.Formats.Bmp.BmpBitsPerPixel.Pixel1;
                img.Mutate(
                    i => i.Resize(250, 250));
                img.SaveAsBmp(imgMemStr, bmpEncoder);

                return Convert.ToBase64String(imgMemStr.ToArray());
            }
            //private static string ConvertImagePNGToBMP(string qrCode)
            //{
            //    string convertedQRCode = qrCode;
            //    byte[] imageBytes = Convert.FromBase64String(qrCode);
            //    using (var ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
            //    {
            //        Image image Image.Load(ms);
            //        using (MemoryStream ms2 new MemoryStream())
            //        {
            //            image.SaveAsBmp(ms2);
            //            var result = ms2.ToArray();
            //            signature = Convert.ToBase64String(result)
            //        }
            //    }
            //    return convertedQRCode;
            //}
            public async Task<string> GetStoreStateName(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string SSName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSSName = new DataSet();
                DataTable dtSSName = null;
                string HSNQuery = @"SELECT STORESTATENAME FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSSName = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtSSName = dsSSName.Tables[0];
                }
                if (dtSSName != null)
                {
                    if (dtSSName.Rows.Count > 0)
                    {
                        SSName = dtSSName.Rows[0]["STORESTATENAME"].ToString();
                        if (string.IsNullOrEmpty(SSName))
                        {
                            SSName = dtSSName.Rows[0]["STORESTATENAME"].ToString();
                        }

                    }
                }


                return SSName;

            }
            public async Task<string> GetCustomerGSTIN(GetSalesTransactionCustomReceiptFieldServiceRequest request, string ITemId)
            {
                string CGSTIN = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCGSTIN = new DataSet();
                DataTable dtCGSTIN = null;
                string HSNQuery = @"select INV.ITEMID,ISNULL(HSN.CODE,'') AS HSNCODE,ISNULL(SAC.SAC,'') AS SACCODE from ax.INVENTTABLE INV
                                    left join ax.HSNCODETABLE_IN HSN on HSN.RECID = INV.HSNCODETABLE_IN and HSN.DATAAREAID = INV.DATAAREAID
                                    left join ax.SERVICEACCOUNTINGCODETABLE_IN  SAC on SAC.RECID = INV.SERVICEACCOUNTINGCODETABLE_IN and SAC.DATAAREAID = INV.DATAAREAID
                                    where INV.ITEMID=@ITEMID ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@ITEMID"] = ITemId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCGSTIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtCGSTIN = dsCGSTIN.Tables[0];
                }
                if (dtCGSTIN != null)
                {
                    if (dtCGSTIN.Rows.Count > 0)
                    {
                        CGSTIN = dtCGSTIN.Rows[0]["HSNCODE"].ToString();
                        if (string.IsNullOrEmpty(CGSTIN))
                        {
                            CGSTIN = dtCGSTIN.Rows[0]["SACCODE"].ToString();
                        }

                    }
                }


                return CGSTIN;

            }
            public async Task<string> GetCustomerPAN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string CPAN = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCPAN = new DataSet();
                DataTable dtCPAN = null;
                string HSNQuery = @"select PANNUMBER from AX.TAXINFORMATIONCUSTTABLE_IN WHERE CUSTTABLE = @CUSTTABLE ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@CUSTTABLE"] = request.SalesOrder.CustomerId;
                // parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCPAN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtCPAN = dsCPAN.Tables[0];
                }
                if (dtCPAN != null)
                {
                    if (dtCPAN.Rows.Count > 0)
                    {
                        CPAN = dtCPAN.Rows[0]["PANNUMBER"].ToString();
                        if (string.IsNullOrEmpty(CPAN))
                        {
                            CPAN = dtCPAN.Rows[0]["PANNUMBER"].ToString();
                        }

                    }
                }


                return CPAN;

            }
            public async Task<string> GetSACCODE_IN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string SAC = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSAC = new DataSet();
                DataTable dtSAC = null;
                string HSNQuery = @"select INV.ITEMID,ISNULL(HSN.CODE,'') AS HSNCODE,ISNULL(SAC.SAC,'') AS SACCODE from ax.INVENTTABLE INV
                                    left join ax.HSNCODETABLE_IN HSN on HSN.RECID = INV.HSNCODETABLE_IN and HSN.DATAAREAID = INV.DATAAREAID
                                    left join ax.SERVICEACCOUNTINGCODETABLE_IN  SAC on SAC.RECID = INV.SERVICEACCOUNTINGCODETABLE_IN and SAC.DATAAREAID = INV.DATAAREAID
                                    where INV.ITEMID=@ITEMID ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@ITEMID"] = request.SalesLine.ItemId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSAC = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtSAC = dsSAC.Tables[0];
                }
                if (dtSAC != null)
                {
                    if (dtSAC.Rows.Count > 0)
                    {
                        SAC = dtSAC.Rows[0]["HSNCODE"].ToString();
                        if (string.IsNullOrEmpty(SAC))
                        {
                            SAC = dtSAC.Rows[1]["SACCODE"].ToString();
                        }

                    }
                }


                return SAC;

            }
            //public async Task<string> GetTAXCOMPONENT_IN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            //{

            //    List<string> TAXINs = new List<string>();

            //    ThrowIf.Null(request, "request");
            //    DataSet dsTAXIN = new DataSet();
            //    DataTable dtTAXIN =null;
            //    string HSNQuery = @"SELECT TAXCOMPONENT FROM ax.RETAILTRANSACTIONTAXTRANSGTE A INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = @TRANSACTIONID";
            //    ParameterSet parameterSet = new ParameterSet();
            //    parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
            //    //parameterSet["@TAXCOMPO"] = TaxComponent;

            //    //parameterSet["@TAXCOMPONENT"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

            //    using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
            //    {
            //        dsTAXIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
            //        dtTAXIN = dsTAXIN.Tables[0];
            //    }
            //    if (dtTAXIN != null && dtTAXIN.Rows.Count > 0)
            //    {
            //        foreach (DataRow row in dtTAXIN.Rows)
            //        {
            //            string TAXIN = row["TAXCOMPONENT"].ToString();
            //            TAXINs.Add(TAXIN);
            //        }
            //    }

            //    return TAXINs;

            //}
            public async Task<List<string>> GetTAXCOMPONENT_IN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                List<string> TAXINs = new List<string>(); // List to store TAXCOMPONENT values

                ThrowIf.Null(request, "request");
                DataSet dsTAXIN = new DataSet();
                DataTable dtTAXIN = null;
                string HSNQuery = @"SELECT TAXCOMPONENT FROM ax.RETAILTRANSACTIONTAXTRANSGTE A INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                                    ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = B.TRANSACTIONID AND A.TRANSACTIONID = @TRANSACTIONID GROUP BY A.TAXCOMPONENT, A.TAXPERCENTAGE";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTAXIN = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTAXIN = dsTAXIN.Tables[0];
                }

                if (dtTAXIN != null && dtTAXIN.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTAXIN.Rows)
                    {
                        string TAXIN = row["TAXCOMPONENT"].ToString();
                        TAXINs.Add(TAXIN);
                    }
                }

                return TAXINs;

            }
            public async Task<List<string>> GetTAXPERC(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                List<string> TAXPERs = new List<string>(); // List to store TAXCOMPONENT values

                ThrowIf.Null(request, "request");
                DataSet dsTAXPER = new DataSet();
                DataTable dtTAXPER = null;
                string HSNQuery = @"SELECT TAXPERCENTAGE FROM ax.RETAILTRANSACTIONTAXTRANSGTE A INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                                    ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = B.TRANSACTIONID AND A.TRANSACTIONID = @TRANSACTIONID GROUP BY A.TAXCOMPONENT, A.TAXPERCENTAGE";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTAXPER = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTAXPER = dsTAXPER.Tables[0];
                }

                if (dtTAXPER != null && dtTAXPER.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTAXPER.Rows)
                    {
                        string TAXPER = row["TAXPERCENTAGE"].ToString();
                        TAXPERs.Add(TAXPER); // Add each TAXCOMPONENT value to the list
                    }
                }
                return TAXPERs;
            }
            public async Task<List<string>> GetTAXBASIS(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                List<string> TAXBASISs = new List<string>(); // List to store TAXCOMPONENT values

                ThrowIf.Null(request, "request");
                DataSet dsTAXBASIS = new DataSet();
                DataTable dtTAXBASIS = null;
                string HSNQuery = @"SELECT SUM(TAXBASIS) AS TAXBASIS  FROM ax.RETAILTRANSACTIONTAXTRANSGTE A INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                                    ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = B.TRANSACTIONID AND A.TRANSACTIONID = @TRANSACTIONID GROUP BY A.TAXCOMPONENT, A.TAXPERCENTAGE";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTAXBASIS = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTAXBASIS = dsTAXBASIS.Tables[0];
                }

                if (dtTAXBASIS != null && dtTAXBASIS.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTAXBASIS.Rows)
                    {
                        string TAXBASIS = row["TAXBASIS"].ToString();
                        TAXBASISs.Add(TAXBASIS); // Add each TAXCOMPONENT value to the list
                    }
                }
                return TAXBASISs;
            }
            public async Task<List<string>> GetTAXAMT(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                List<string> TAXAMOUNTs = new List<string>(); // List to store TAXCOMPONENT values

                ThrowIf.Null(request, "request");
                DataSet dsTAXAMOUNT = new DataSet();
                DataTable dtTAXAMOUNT = null;
                string HSNQuery = @"SELECT SUM(A.TAXAMOUNT) AS TAXAMOUNT  FROM ax.RETAILTRANSACTIONTAXTRANSGTE A INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                                    ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = B.TRANSACTIONID AND A.TRANSACTIONID = @TRANSACTIONID GROUP BY A.TAXCOMPONENT, A.TAXPERCENTAGE";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTAXAMOUNT = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtTAXAMOUNT = dsTAXAMOUNT.Tables[0];
                }

                if (dtTAXAMOUNT != null && dtTAXAMOUNT.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTAXAMOUNT.Rows)
                    {
                        string TAXAMOUNT = row["TAXAMOUNT"].ToString();
                        TAXAMOUNTs.Add(TAXAMOUNT); // Add each TAXCOMPONENT value to the list
                    }
                }
                return TAXAMOUNTs;
            }
            public async Task<string> GetSalesGroupName(GetSalesTransactionCustomReceiptFieldServiceRequest request, string ITemId)
            {


                string HSNCode = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsHSNInfo = new DataSet();
                DataTable dtHSNInfo = null;
                string HSNQuery = @"select INV.ITEMID,ISNULL(HSN.CODE,'') AS HSNCODE,ISNULL(SAC.SAC,'') AS SACCODE from ax.INVENTTABLE INV
                                    left join ax.HSNCODETABLE_IN HSN on HSN.RECID = INV.HSNCODETABLE_IN and HSN.DATAAREAID = INV.DATAAREAID
                                    left join ax.SERVICEACCOUNTINGCODETABLE_IN  SAC on SAC.RECID = INV.SERVICEACCOUNTINGCODETABLE_IN and SAC.DATAAREAID = INV.DATAAREAID
                                    where INV.ITEMID=@ITEMID ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@ITEMID"] = ITemId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsHSNInfo = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtHSNInfo = dsHSNInfo.Tables[0];
                }
                if (dtHSNInfo != null)
                {
                    if (dtHSNInfo.Rows.Count > 0)
                    {
                        HSNCode = dtHSNInfo.Rows[0]["HSNCODE"].ToString();
                        if (string.IsNullOrEmpty(HSNCode))
                        {
                            HSNCode = dtHSNInfo.Rows[0]["SACCODE"].ToString();
                        }

                    }
                }
                return HSNCode;
            }
            public async Task<string> GetCustomerMobileNo(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string Phone = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsPHONE = new DataSet();
                DataTable dtPHONE = null;
                string PHONEQuery = @"select i.PHONE AS PHONE from ax.CUSTTABLE a INNER JOIN (Select Locator as PHONE,ACCOUNTNUM from ax.logisticsElectronicAddress p 
                                    INNER join ax.dirPartyLocation q on p.LOCATION =q.LOCATION INNER JOIN ax.CUSTTABLE r on q.PARTY=r.PARTY 
                                    where p.ISPRIMARY=1 and p.TYPE=1 ) i on i.ACCOUNTNUM= a.ACCOUNTNUM where a.ACCOUNTNUM = @ACCOUNTNUM ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@ACCOUNTNUM"] = request.SalesOrder.CustomerId;
                // parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsPHONE = await databaseContext.ExecuteQueryDataSetAsync(PHONEQuery, parameterSet).ConfigureAwait(false);
                    dtPHONE = dsPHONE.Tables[0];
                }
                if (dtPHONE != null)
                {
                    if (dtPHONE.Rows.Count > 0)
                    {
                        Phone = dtPHONE.Rows[0]["PHONE"].ToString();
                        if (string.IsNullOrEmpty(Phone))
                        {
                            Phone = dtPHONE.Rows[0]["PHONE"].ToString();
                        }

                    }
                }
                return Phone;
            }
            public async Task<string> GetDeliveryStateName(GetSalesTransactionCustomReceiptFieldServiceRequest request, string ITemId)
            {
                string HSNCode = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsHSNInfo = new DataSet();
                DataTable dtHSNInfo = null;
                string HSNQuery = @"select INV.ITEMID,ISNULL(HSN.CODE,'') AS HSNCODE,ISNULL(SAC.SAC,'') AS SACCODE from ax.INVENTTABLE INV
                                    left join ax.HSNCODETABLE_IN HSN on HSN.RECID = INV.HSNCODETABLE_IN and HSN.DATAAREAID = INV.DATAAREAID
                                    left join ax.SERVICEACCOUNTINGCODETABLE_IN  SAC on SAC.RECID = INV.SERVICEACCOUNTINGCODETABLE_IN and SAC.DATAAREAID = INV.DATAAREAID
                                    where INV.ITEMID=@ITEMID ";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@ITEMID"] = ITemId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsHSNInfo = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtHSNInfo = dsHSNInfo.Tables[0];
                }
                if (dtHSNInfo != null)
                {
                    if (dtHSNInfo.Rows.Count > 0)
                    {
                        HSNCode = dtHSNInfo.Rows[0]["HSNCODE"].ToString();
                        if (string.IsNullOrEmpty(HSNCode))
                        {
                            HSNCode = dtHSNInfo.Rows[0]["SACCODE"].ToString();
                        }

                    }
                }
                return HSNCode;
            }
            public async Task<string> GetFranchiseeHOAdd(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string FRANAdd = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsFRANAdd = new DataSet();
                DataTable dtFRANAdd = null;
                string HSNQuery = @"SELECT FRANCHISEEHOADD FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsFRANAdd = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtFRANAdd = dsFRANAdd.Tables[0];
                }
                if (dtFRANAdd != null)
                {
                    if (dtFRANAdd.Rows.Count > 0)
                    {
                        FRANAdd = dtFRANAdd.Rows[0]["FRANCHISEEHOADD"].ToString();
                        if (string.IsNullOrEmpty(FRANAdd))
                        {
                            FRANAdd = dtFRANAdd.Rows[0]["FRANCHISEEHOADD"].ToString();
                        }

                    }
                }


                return FRANAdd;

            }
            public async Task<string> GetFranchiseeCIN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string FRANCin = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsFRANCin = new DataSet();
                DataTable dtFRANCin = null;
                string HSNQuery = @"SELECT FRANCHISEECIN FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsFRANCin = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtFRANCin = dsFRANCin.Tables[0];
                }
                if (dtFRANCin != null)
                {
                    if (dtFRANCin.Rows.Count > 0)
                    {
                        FRANCin = dtFRANCin.Rows[0]["FRANCHISEECIN"].ToString();
                        if (string.IsNullOrEmpty(FRANCin))
                        {
                            FRANCin = dtFRANCin.Rows[0]["FRANCHISEECIN"].ToString();
                        }

                    }
                }


                return FRANCin;

            }
            public async Task<string> GetPaymentRefNo(GetSalesTransactionCustomReceiptFieldServiceRequest request, TenderLine tenderLine)
            {
                ParameterSet parameterSet = new ParameterSet();
                string PaymentRefNo = string.Empty;
                string gvcode = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsPAYInfo = new DataSet();
                DataTable dtPAYInfo = null;
                using (SqlServerDatabaseContext databaseContext = new SqlServerDatabaseContext(request.RequestContext))
                {
                    string query = @"SELECT * FROM EXT.ACXINTEGRATIONPAYMENTLOG WHERE TRANSACTIONID = @TRANSACTIONID AND TENDERID ='59'
                                         AND TERMINAL =@TERMINAL AND STORE =@STORE";

                    parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                    parameterSet["@TERMINAL"] = request.RequestContext.GetDeviceConfiguration().TerminalId;
                    parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                    parameterSet["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                    DataSet dataser = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    if (dataser != null && dataser.Tables[0].Rows.Count > 0)
                    {
                        if (HelperClass.CheckDataSet(dataser) == 3)
                        {
                            if (!string.IsNullOrEmpty(dataser.Tables[0].Rows[0]["UNIQUETRANSID"].ToString()))
                            {
                                gvcode = dataser.Tables[0].Rows[0]["UNIQUETRANSID"].ToString();
                                string rtstQuery = @"UPDATE AX.RETAILTRANSACTIONPAYMENTTRANS SET PAYMENTAUTHORIZATION =@CNNUMBER
                                            WHERE TRANSACTIONID =@TRANSACTIONID AND TENDERTYPE ='59' AND RECEIPTID =@RECEIPTID AND PAYMENTAUTHORIZATION = '' 
                                            and STORE =@STORE and TERMINAL =@TERMINAL AND DATAAREAID =@DATAAREAID";

                                parameterSet["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                                parameterSet["@TERMINAL"] = request.RequestContext.GetDeviceConfiguration().TerminalId;
                                parameterSet["@CNNUMBER"] = gvcode;
                                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                                await databaseContext.ExecuteQueryDataSetAsync(rtstQuery, parameterSet).ConfigureAwait(false);
                            }
                        }
                    }
                }
                //          string HSNQuery = @"
                //                  SELECT CASE WHEN REFERENCE!='' THEN TENDERNAME + '-' + REFERENCE ELSE TENDERNAME END REFERENCE FROM (
                //                  SELECT  CASE WHEN A.PAYMENTAUTHORIZATION!=''  THEN SUBSTRING(ISNULL(A.PAYMENTAUTHORIZATION, ''),0,30) 
                //                  ELSE SUBSTRING(ISNULL(C.INFORMATION, ''),0,30) END AS REFERENCE,
                //                  CASE WHEN ISNULL(A.CARDTYPEID, '-')!='-' AND A.CARDTYPEID!='' THEN SUBSTRING(ISNULL(A.CARDTYPEID, ''),0,10)  ELSE 
                //CASE WHEN CHANGELINE=1 THEN 'Change Back' else  SUBSTRING(ISNULL(B.NAME, ''),0,10) end END TENDERNAME
                //                  FROM ax.RETAILTRANSACTIONPAYMENTTRANS A LEFT JOIN AX.RETAILTRANSACTIONINFOCODETRANS C ON 
                //                  C.TRANSACTIONID = A.TRANSACTIONID AND C.PARENTLINENUM = A.LINENUM  AND C.INFOCODEID = 'RefNo.' And C.INPUTTYPE = '0'
                //                  LEFT JOIN ax.RETAILTENDERTYPETABLE B ON B.TENDERTYPEID = A.TENDERTYPE 
                //                  WHERE A.TRANSACTIONID = @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID AND A.VOIDSTATUS = 0 AND A.LINENUM = @LINENUM
                //                  ) AS A";
                string HSNQuery = @"SELECT CASE WHEN REFERENCE != '' THEN MAX(TENDERNAME) + '- ' + REFERENCE ELSE MAX(TENDERNAME)
                            END AS REFERENCE FROM (SELECT CASE WHEN A.PAYMENTAUTHORIZATION != '' THEN SUBSTRING(ISNULL(A.PAYMENTAUTHORIZATION, ''), 0, 30)
                            when A.CREDITVOUCHERID !=''  and A.CHANGELINE = 0 then ISNULL(A.CREDITVOUCHERID,'')--GURU
                            ELSE SUBSTRING(ISNULL(C.INFORMATION, ''), 0, 30) END AS REFERENCE, CASE WHEN ISNULL(A.CARDTYPEID, '-') != '-' AND A.CARDTYPEID != ''
                            THEN SUBSTRING(ISNULL(A.CARDTYPEID, ''), 0, 10) ELSE CASE WHEN CHANGELINE = 1 THEN SUBSTRING(ISNULL(D.CHANGELINEONRECEIPT,''),0,15)
                            + '- '+ ISNULL(A.CREDITVOUCHERID,'')--GURU
                            ELSE SUBSTRING(ISNULL(B.NAME, ''), 0, 20) END END AS TENDERNAME FROM ax.RETAILTRANSACTIONPAYMENTTRANS A
                            LEFT JOIN AX.RETAILTRANSACTIONINFOCODETRANS C ON C.TRANSACTIONID = A.TRANSACTIONID AND
                            C.PARENTLINENUM = A.LINENUM AND C.INFOCODEID = 'Ref No.' AND C.INPUTTYPE in('0','9')
                            LEFT JOIN ax.RETAILTENDERTYPETABLE B ON B.TENDERTYPEID = A.TENDERTYPE
                            JOIN ax.RetailStoreTenderTypeTable D ON D.TENDERTYPEID = B.TENDERTYPEID 
                            WHERE A.TRANSACTIONID =  @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID AND
                            A.VOIDSTATUS = 0 AND A.LINENUM =  @LINENUM) AS A GROUP BY REFERENCE";

                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                parameterSet["@LINENUM"] = tenderLine.LineNumber;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsPAYInfo = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtPAYInfo = dsPAYInfo.Tables[0];
                }
                if (dtPAYInfo != null)
                {
                    if (dtPAYInfo.Rows.Count > 0)
                    {
                        PaymentRefNo = dtPAYInfo.Rows[0]["REFERENCE"].ToString();
                    }
                }
                return PaymentRefNo;
            }
            public async Task<string> GetCouID(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string Id = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsId = new DataSet();
                DataTable dtId = null;
                string HSNQuery = @"SELECT COUPONNUMBER,DISCLAIMER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsId = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtId = dsId.Tables[0];
                }
                if (dtId != null)
                {
                    if (dtId.Rows.Count > 0)
                    {
                        Id = dtId.Rows[0]["COUPONNUMBER"].ToString();
                    }
                }
                return Id;
            }
            public async Task<string> GetCouponDISCLAIMER(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string wrappedDisclaimer = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsId = new DataSet();
                DataTable dtId = null;
                string HSNQuery = @"SELECT DISCLAIMER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsId = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtId = dsId.Tables[0];
                }
                if (dtId != null)
                {
                    if (dtId.Rows.Count > 0)
                    {
                        wrappedDisclaimer = dtId.Rows[0]["DISCLAIMER"].ToString();
                        wrappedDisclaimer = WrapWords(wrappedDisclaimer, 45);
                    }
                }
                return wrappedDisclaimer;
            }
            static string WrapWords(string input, int maxLength)
            {
                string[] words = input.Split(' ');
                string wrappedText = "";
                int currentLength = 0;

                foreach (string word in words)
                {
                    if (currentLength + word.Length > maxLength)
                    {
                        wrappedText += Environment.NewLine;
                        currentLength = 0;
                    }

                    wrappedText += word + " ";
                    currentLength += word.Length + 1; // +1 for the space
                }

                return wrappedText.Trim();
            }
            public async Task<string> GetCouponDISCLAIMERArray(GetSalesTransactionCustomReceiptFieldServiceRequest request, int count)
            {
                string Id = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsId = new DataSet();
                DataTable dtId = null;
                string HSNQuery = @"SELECT DISCLAIMER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsId = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtId = dsId.Tables[0];
                    if (dtId != null)
                    {
                        if (dtId.Rows.Count > 0)
                        {
                            HSNQuery = "EXEC EXT.ACXSPLITWITHSPACE @TEXTVALUE,@INLEN";
                            ParameterSet parameters = new ParameterSet();
                            parameters["@TEXTVALUE"] = dtId.Rows[0]["DISCLAIMER"].ToString();
                            parameters["@INLEN"] = 52;
                            DataSet ds = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameters).ConfigureAwait(false);
                            if (ds != null)
                            {
                                if (ds.Tables.Count > 0)
                                {
                                    if (ds.Tables[0].Rows.Count >= count)
                                        Id = ds.Tables[0].Rows[count]["STRVALUE"].ToString();
                                }
                            }
                            //Id = dtId.Rows[0]["DISCLAIMER"].ToString();
                        }
                    }
                }

                return Id;
            }
            public async Task<string> GetDisValue(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Dis = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsDis = new DataSet();
                DataTable dtDis = null;
                string HSNQuery = @"SELECT  FORMAT(DISCOUNTVALUE,'0.00') as DISCOUNTVALUE  from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsDis = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtDis = dsDis.Tables[0];
                }
                if (dtDis != null)
                {
                    if (dtDis.Rows.Count > 0)
                    {
                        Dis = dtDis.Rows[0]["DISCOUNTVALUE"].ToString();
                    }
                }
                return Dis;
            }
            public async Task<string> GetExp(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Exp = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsExp = new DataSet();
                DataTable dtExp = null;
                string HSNQuery = @"SELECT FORMAT(EXPIRYDATE, 'dd-MM-yyyy') AS EXPIRYDATE
                                    FROM ext.ACXCASHBACKCOUPONDETAILS
                                    WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsExp = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtExp = dsExp.Tables[0];
                }
                if (dtExp != null)
                {
                    if (dtExp.Rows.Count > 0)
                    {
                        Exp = dtExp.Rows[0]["EXPIRYDATE"].ToString();
                    }
                }
                return Exp;
            }
            public async Task<string> GetNCouID(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Id = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsId = new DataSet();
                DataTable dtId = null;
                string HSNQuery = @"SELECT COUPONNUMBER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsId = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtId = dsId.Tables[0];
                }
                if (dtId != null)
                {
                    if (dtId.Rows.Count > 0)
                    {
                        Id = "Coupon ID";
                    }
                }
                return Id;
            }
            public async Task<string> GetNDisValue(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Dis = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsDis = new DataSet();
                DataTable dtDis = null;
                string HSNQuery = @"SELECT DISCOUNTVALUE from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsDis = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtDis = dsDis.Tables[0];
                }
                if (dtDis != null)
                {
                    if (dtDis.Rows.Count > 0)
                    {
                        Dis = "Coupon Value";
                    }
                }
                return Dis;
            }
            public async Task<string> GetNExp(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                string Exp = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsExp = new DataSet();
                DataTable dtExp = null;
                string HSNQuery = @"SELECT EXPIRYDATE from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsExp = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtExp = dsExp.Tables[0];
                }
                if (dtExp != null)
                {
                    if (dtExp.Rows.Count > 0)
                    {
                        Exp = "Expire Date";
                    }
                }
                return Exp;
            }
            public async Task<string> GeTnCoupDetail(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                string Det = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsDet = new DataSet();
                DataTable dtDet = null;
                string HSNQuery = @"SELECT COUPONNUMBER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtDet = dsDet.Tables[0];
                }
                if (dtDet != null)
                {
                    if (dtDet.Rows.Count > 0)
                    {
                        Det = "Coupon Detail";
                    }
                }
                return Det;
            }
            public async Task<string> GeTHeader(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Det = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsDet = new DataSet();
                DataTable dtDet = null;
                string HSNQuery = @"SELECT COUPONNUMBER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtDet = dsDet.Tables[0];
                }
                if (dtDet != null)
                {
                    if (dtDet.Rows.Count > 0)
                    {
                        Det = "--------------------------------------------------";
                    }
                }
                return Det;
            }
            public async Task<string> GetFooter(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string Det = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsDet = new DataSet();
                DataTable dtDet = null;
                string HSNQuery = @"SELECT COUPONNUMBER from ext.ACXCASHBACKCOUPONDETAILS WHERE RECEIPTID = @RECEIPTID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsDet = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtDet = dsDet.Tables[0];
                }
                if (dtDet != null)
                {
                    if (dtDet.Rows.Count > 0)
                    {
                        Det = "--------------------------------------------------";
                    }
                }
                return Det;
            }
            public async Task<string> AllTaxDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                ThrowIf.Null(request, "request");

                DataSet dsTaxDetails = new DataSet();
                DataTable dtTaxDetails = null;

                string query = @"SELECT A.TAXCOMPONENT,
                        FORMAT(A.TAXPERCENTAGE,'0.00') AS TAXPERCENTAGE,
                        FORMAT(A.TAXBASIS,'0.00') AS TAXBASIS,
                        FORMAT(A.TAXAMOUNT,'0.00') AS TAXAMOUNT
                        FROM ax.RETAILTRANSACTIONTAXTRANSGTE A
                        INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                        ON A.SALELINENUM = B.LINENUM
                        WHERE A.TRANSACTIONID = B.TRANSACTIONID
                        AND A.TRANSACTIONID = @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID
                        GROUP BY TAXCOMPONENT, TAXPERCENTAGE,TAXBASIS,A.TAXAMOUNT";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTaxDetails = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    dtTaxDetails = dsTaxDetails.Tables[0];
                }

                StringBuilder resultBuilder = new StringBuilder();

                if (dtTaxDetails != null && dtTaxDetails.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTaxDetails.Rows)
                    {
                        string taxComponentString = row["TAXCOMPONENT"].ToString();
                        string taxPercentageString = row["TAXPERCENTAGE"].ToString();
                        string taxBasisString = row["TAXBASIS"].ToString();
                        string taxAmountString = row["TAXAMOUNT"].ToString();

                        string formattedLine = $"{taxComponentString}\t{taxPercentageString}\t{taxBasisString}\t{taxAmountString}";
                        resultBuilder.AppendLine(formattedLine);
                    }
                }

                return resultBuilder.ToString();
            }
            public async Task<string> GetQRTaxDetails(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                ThrowIf.Null(request, "request");

                DataSet dsTaxDetails = new DataSet();
                DataTable dtTaxDetails = null;

                string query = @"SELECT A.TAXCOMPONENT,
                        FORMAT(A.TAXPERCENTAGE,'0.00') AS TAXPERCENTAGE,
                        FORMAT(SUM(A.TAXBASIS),'0.00') AS TAXBASIS,
                        FORMAT(SUM(A.TAXAMOUNT),'0.00') AS TAXAMOUNT
                        FROM ax.RETAILTRANSACTIONTAXTRANSGTE A
                        INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B 
                        ON A.SALELINENUM = B.LINENUM
                        WHERE A.TRANSACTIONID = B.TRANSACTIONID
                        AND A.TRANSACTIONID = @TRANSACTIONID AND A.DATAAREAID = @DATAAREAID AND B.TRANSACTIONSTATUS=0
                        GROUP BY TAXCOMPONENT, TAXPERCENTAGE";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTaxDetails = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    dtTaxDetails = dsTaxDetails.Tables[0];
                }

                StringBuilder resultBuilder = new StringBuilder();

                if (dtTaxDetails != null && dtTaxDetails.Rows.Count > 0)
                {
                    foreach (DataRow row in dtTaxDetails.Rows)
                    {
                        string taxComponentString = row["TAXCOMPONENT"].ToString();
                        string taxPercentageString = row["TAXPERCENTAGE"].ToString();
                        string taxBasisString = row["TAXBASIS"].ToString();
                        string taxAmountString = row["TAXAMOUNT"].ToString();

                        string formattedLine = $"\n{taxComponentString}\t{taxPercentageString}\t{taxBasisString}\t{taxAmountString}";
                        resultBuilder.AppendLine(formattedLine);
                    }
                }

                return resultBuilder.ToString();
            }
            public async Task<string> GetCardType(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                string CardType = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCardType = new DataSet();
                DataTable dtCardType = null;
                string HSNQuery = @"select CARDTYPE from ext.ACXRETAILTRANSACTIONPAYMENTTRANS WHERE TRANSACTIONID  = @TRANSACTIONID AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;



                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCardType = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtCardType = dsCardType.Tables[0];
                }
                if (dtCardType != null)
                {
                    if (dtCardType.Rows.Count > 0)
                    {
                        CardType = dtCardType.Rows[0]["CARDTYPE"].ToString();
                    }
                }

                return CardType;
            }
            public async Task<string> GetEntityName(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string EntityName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsEntityName = new DataSet();
                DataTable dtEntityName = null;
                string Query = @"SELECT EntityAddress FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsEntityName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtEntityName = dsEntityName.Tables[0];
                }
                if (dtEntityName != null)
                {
                    if (dtEntityName.Rows.Count > 0)
                    {
                        EntityName = dtEntityName.Rows[0]["EntityAddress"].ToString();
                        if (string.IsNullOrEmpty(EntityName))
                        {
                            EntityName = dtEntityName.Rows[0]["EntityAddress"].ToString();
                        }

                    }
                }
                return EntityName;

            }
            public async Task<string> GetEntityNamet(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string EntityName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsEntityName = new DataSet();
                DataTable dtEntityName = null;
                string Query = @"SELECT EntityAddress2 FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsEntityName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtEntityName = dsEntityName.Tables[0];
                }
                if (dtEntityName != null)
                {
                    if (dtEntityName.Rows.Count > 0)
                    {
                        EntityName = dtEntityName.Rows[0]["EntityAddress2"].ToString();
                        if (string.IsNullOrEmpty(EntityName))
                        {
                            EntityName = dtEntityName.Rows[0]["EntityAddress2"].ToString();
                        }

                    }
                }
                return EntityName;

            }
            public async Task<string> GetEntityNameadd(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string EntityName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsEntityName = new DataSet();
                DataTable dtEntityName = null;
                string Query = @"SELECT EntityAddress3 FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsEntityName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtEntityName = dsEntityName.Tables[0];
                }
                if (dtEntityName != null)
                {
                    if (dtEntityName.Rows.Count > 0)
                    {
                        EntityName = dtEntityName.Rows[0]["EntityAddress3"].ToString();
                        if (string.IsNullOrEmpty(EntityName))
                        {
                            EntityName = dtEntityName.Rows[0]["EntityAddress3"].ToString();
                        }

                    }
                }
                return EntityName;

            }
            public async Task<string> GetEntity(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string EntityName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsEntityName = new DataSet();
                DataTable dtEntityName = null;
                string Query = @"SELECT EntityName FROM EXT.ACXSTOREINFO WHERE STORENUMBER  = @STORENUMBER AND DATAAREAID = @DATAAREAID";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;


                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsEntityName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtEntityName = dsEntityName.Tables[0];
                }
                if (dtEntityName != null)
                {
                    if (dtEntityName.Rows.Count > 0)
                    {
                        EntityName = dtEntityName.Rows[0]["EntityName"].ToString();
                        if (string.IsNullOrEmpty(EntityName))
                        {
                            EntityName = dtEntityName.Rows[0]["EntityName"].ToString();
                        }

                    }
                }
                return EntityName;

            }
            public async Task<string> GetGrossAmt(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string GrossAmt = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsGrossAmt = new DataSet();
                DataTable dtGrossAmt = null;

                string HSNQuery = @"SELECT ORIGINALPRICE, QTY, TRANSACTIONID, LINENUM FROM AX.RETAILTRANSACTIONSALESTRANS 
                        WHERE TRANSACTIONID = @TRANSACTIONID AND DATAAREAID = @DATAAREAID";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsGrossAmt = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtGrossAmt = dsGrossAmt.Tables[0];
                }

                if (dtGrossAmt != null && dtGrossAmt.Rows.Count > 0)
                {
                    decimal totalGrossAmt = 0;

                    foreach (DataRow row in dtGrossAmt.Rows)
                    {
                        // Retrieve values from the DataRow for each line
                        decimal originalPrice = Convert.ToDecimal(row["ORIGINALPRICE"]);
                        decimal qty = Convert.ToDecimal(row["QTY"]);

                        // Calculate the GrossAmt for each line and accumulate the total
                        totalGrossAmt += originalPrice * qty * -1;
                    }

                    // Format the total with two decimal places and assign it to GrossAmt
                    GrossAmt = totalGrossAmt.ToString("0.00");
                }

                return GrossAmt;
            }
            public async Task<string> AllTaxDetailsWithItem(SalesLine salesLine, GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                ThrowIf.Null(request, "request");

                DataSet dsTaxDetails = new DataSet();
                DataTable dtTaxDetails = null;

                string query = @"SELECT FORMAT (SUM((B.TAXPERCENTAGE)), '0.00') AS TaxPerc  FROM AX.RETAILTRANSACTIONTAXTRANSGTE B
                                WHERE B.TRANSACTIONID = @TRANSACTIONID  AND B.SALELINENUM = @SALELINENUM
                                AND B.DATAAREAID = @DATAAREAID ";
                //string query = @"SELECT TAXCOMPONENT,FORMAT(A.TAXPERCENTAGE,'0.00') AS TaxPerc,FORMAT(A.TAXBASIS,'0.00') AS TAXBASIS  
                //                 FROM  ax.RETAILTRANSACTIONTAXTRANSGTE A  INNER JOIN ax.RETAILTRANSACTIONSALESTRANS B ON A.SALELINENUM = B.LINENUM WHERE A.TRANSACTIONID = B.TRANSACTIONID
                //                 AND A.TRANSACTIONID =  @TRANSACTIONID   AND A.DATAAREAID =  @DATAAREAID AND A.SALELINENUM = @SALELINENUM GROUP BY   TAXCOMPONENT, TAXPERCENTAGE, TAXBASIS  ";


                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                parameterSet["@SALELINENUM"] = salesLine.LineNumber;
                // parameterSet["@SALELINENUM"] = request.SalesOrder.ActiveSalesLines[0].LineNumber;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsTaxDetails = await databaseContext.ExecuteQueryDataSetAsync(query, parameterSet).ConfigureAwait(false);
                    dtTaxDetails = dsTaxDetails.Tables[0];
                }

                StringBuilder resultBuilder = new StringBuilder();

                if (dtTaxDetails != null && dtTaxDetails.Rows.Count > 0)
                {
                    string formattedLine = "";
                    string taxPercentageString = string.Empty;
                    foreach (DataRow row in dtTaxDetails.Rows)
                    {
                        if (row["TaxPerc"] != null)
                        {
                            taxPercentageString = row["TaxPerc"].ToString();
                        }
                        formattedLine = $"GST {taxPercentageString.Replace(" ", "")}%";

                    }
                    resultBuilder.AppendLine(formattedLine);
                }

                return resultBuilder.ToString();
            }
            public async Task<string> syncData(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {// changes by manikant 05-04-2024  
                ParameterSet parameterSet = new ParameterSet();
                string transactionId = string.Empty;
                string PaymentRefNo = string.Empty;
                string GVCODE = string.Empty;
                ThrowIf.Null(request, "request");
                transactionId = request.SalesOrder.Id;
                string retailTransactionPaymentTrans = string.Empty;
                using (SqlServerDatabaseContext databaseContext = new SqlServerDatabaseContext(request.RequestContext))
                {
                    string qryRtpt = @"SELECT RECEIPTID,TRANSTIME,AMOUNTTENDERED,TENDERTYPE,LINENUM,CREATEDDATETIME  FROM AX.RETAILTRANSACTIONPAYMENTTRANS where TRANSACTIONID=@TRANSACTIONID AND STORE=@STORE AND DATAAREAID=@DATAAREAID";
                    ParameterSet para = new ParameterSet();
                    para["@TRANSACTIONID"] = transactionId;
                    para["STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                    para["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                    DataSet qrrtpt = await databaseContext.ExecuteQueryDataSetAsync(qryRtpt, para).ConfigureAwait(false);
                    string rtptReceiptId = "";

                    if (qrrtpt != null && qrrtpt.Tables[0].Rows.Count > 0)
                    {
                        rtptReceiptId = qrrtpt.Tables[0].Rows[0]["RECEIPTID"].ToString();

                    }
                    ParameterSet paramIPL = new ParameterSet();
                    string query = @"SELECT UNIQUETRANSID,*  FROM EXT.ACXINTEGRATIONPAYMENTLOG WHERE TRANSACTIONID = @TRANSACTIONID AND TENDERID ='59'
                                          AND STORE =@STORE AND DATAAREAID=@DATAAREAID";

                    paramIPL["@TRANSACTIONID"] = transactionId;
                    paramIPL["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                    paramIPL["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                    DataSet dataser = await databaseContext.ExecuteQueryDataSetAsync(query, paramIPL).ConfigureAwait(false);
                    if (dataser != null && dataser.Tables[0].Rows.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(dataser.Tables[0].Rows[0]["UNIQUETRANSID"].ToString()))
                        {
                            GVCODE = dataser.Tables[0].Rows[0]["UNIQUETRANSID"].ToString();
                            if (dataser != null && dataser.Tables[0].Rows.Count > 0)
                            {

                                string rtstQuery = @"UPDATE AX.RETAILTRANSACTIONPAYMENTTRANS SET PAYMENTAUTHORIZATION =@CNNUMBER
                                            WHERE TRANSACTIONID =@TRANSACTIONID AND TENDERTYPE ='59' AND RECEIPTID =@RECEIPTID AND PAYMENTAUTHORIZATION = '' 
                                            and STORE =@STORE AND DATAAREAID =@DATAAREAID";

                                parameterSet["@CNNUMBER"] = GVCODE;
                                parameterSet["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                                parameterSet["@TRANSACTIONID"] = transactionId;
                                parameterSet["@RECEIPTID"] = rtptReceiptId;
                                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                                await databaseContext.ExecuteQueryDataSetAsync(rtstQuery, parameterSet).ConfigureAwait(false);

                            }
                        }
                    }
                    ParameterSet paramArtpt = new ParameterSet();
                    string qryArtpt = @"SELECT TRANSACTIONID  FROM ext.ACXRETAILTRANSACTIONPAYMENTTRANS where TRANSACTIONID=@TRANSACTIONID AND DATAAREAID=@DATAAREAID";
                    paramArtpt["@TRANSACTIONID"] = transactionId;
                    paramArtpt["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                    DataSet dsArtps = await databaseContext.ExecuteQueryDataSetAsync(qryArtpt, paramArtpt).ConfigureAwait(false);
                    if (dsArtps != null && dsArtps.Tables[0].Rows.Count > 0)
                    {
                        retailTransactionPaymentTrans = "1";
                    }
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        if (retailTransactionPaymentTrans != "1")
                        {
                            if (qrrtpt != null && qrrtpt.Tables[0].Rows.Count > 0)
                            {
                                foreach (DataRow drrtpt in qrrtpt.Tables[0].Rows)
                                {
                                    string strQryInsert = @"INSERT INTO EXT.ACXRETAILTRANSACTIONPAYMENTTRANS
                                                    (TERMINAL,CHANNEL,STORE,STAFFID,TRANSTIME,RemainingBalance,PAYMENTREFERENCEID,TRANSACTIONID,LINENUM,
                                                    AUTHENTICATIONCODE,CARDTYPE,CARDNUMBER,ACQUIRERNAME,PAYMENTGATEWAY,CARDHOLDERNAME,
                                                    BANKNAME,TRANSDATETIME,DATAAREAID,ACQUIRERID,RESPONSEJSON,PAYMENTRANSACTIONID,OTP,TENDERTYPE) VALUES 
                                                    (@TERMINAL,@CHANNEL,@STORE,@STAFFID,@TRANSTIME,@RemainingBalance,@PAYMENTREFERENCEID,@TRANSACTIONID,@LINENUM,
                                                    @AUTHENTICATIONCODE,@CARDTYPE,@CARDNUMBER,@ACQUIRERNAME,@PAYMENTGATEWAY,@CARDHOLDERNAME,
                                                    @BANKNAME,@TRANSDATETIME,@DATAAREAID,@ACQUIRERID,@RESPONSEJSON,@UNIQUEREFERENCEID,@OTP,@TENDERTYPE)";
                                    ParameterSet parameterRtptTable = new ParameterSet();
                                    parameterRtptTable["@TERMINAL"] = request.RequestContext.GetDeviceConfiguration().TerminalId;
                                    parameterRtptTable["@CHANNEL"] = request.RequestContext.GetDeviceConfiguration().ChannelId;
                                    parameterRtptTable["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                                    parameterRtptTable["@STAFFID"] = request.RequestContext.Runtime.CurrentPrincipal.UserId;
                                    parameterRtptTable["@TRANSTIME"] = drrtpt["TRANSTIME"].ToString();
                                    parameterRtptTable["@RemainingBalance"] = !String.IsNullOrWhiteSpace(drrtpt["AMOUNTTENDERED"].ToString()) ? Convert.ToDecimal(drrtpt["AMOUNTTENDERED"].ToString()) : 0;
                                    parameterRtptTable["@PAYMENTREFERENCEID"] = drrtpt["TENDERTYPE"].ToString() == "59" ? GVCODE : "";
                                    parameterRtptTable["@TRANSACTIONID"] = transactionId;
                                    parameterRtptTable["@LINENUM"] = drrtpt["LINENUM"];
                                    parameterRtptTable["@AUTHENTICATIONCODE"] = "";
                                    parameterRtptTable["@CARDTYPE"] = "";
                                    parameterRtptTable["@CARDNUMBER"] = "";
                                    parameterRtptTable["@ACQUIRERID"] = "";
                                    parameterRtptTable["@ACQUIRERNAME"] = "";
                                    parameterRtptTable["@RESPONSEJSON"] = "";
                                    parameterRtptTable["@PAYMENTGATEWAY"] = "";
                                    parameterRtptTable["@CARDHOLDERNAME"] = "";
                                    parameterRtptTable["@BANKNAME"] = "";
                                    parameterRtptTable["@UNIQUEREFERENCEID"] = "";
                                    parameterRtptTable["@TRANSDATETIME"] = Convert.ToDateTime(drrtpt["CREATEDDATETIME"].ToString());
                                    parameterRtptTable["@OTP"] = "";
                                    parameterRtptTable["@TENDERTYPE"] = drrtpt["TENDERTYPE"].ToString();
                                    parameterRtptTable["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                                    await databaseContext.ExecuteQueryDataSetAsync(strQryInsert, parameterRtptTable).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                return "SUCESS";
            }
            public async Task<string> GetRedeemCN(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {


                string RedeemCreditNo = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsRedeemCreditNo = new DataSet();
                DataTable dtRedeemCreditNo = null;
                string HSNQuery = @"SELECT CREDITVOUCHERID, * from ax.RetailTransactionPaymentTrans where
                                    TRANSACTIONID = @TRANSACTIONID AND CHANGELINE = 0";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                //parameterSet["@LINENUM"] = salesLine.LineNumber;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsRedeemCreditNo = await databaseContext.ExecuteQueryDataSetAsync(HSNQuery, parameterSet).ConfigureAwait(false);
                    dtRedeemCreditNo = dsRedeemCreditNo.Tables[0];
                }
                if (dtRedeemCreditNo != null)
                {
                    GlobalVariablesCounter.GlobalCreditNoteTotal = dtRedeemCreditNo.Rows.Count;
                    if (dtRedeemCreditNo.Rows.Count > 0)
                    {
                        if (GlobalVariablesCounter.GlobalCreditNoteCount < GlobalVariablesCounter.GlobalCreditNoteTotal)
                        {
                            RedeemCreditNo = dtRedeemCreditNo.Rows[GlobalVariablesCounter.GlobalCreditNoteCount]["CREDITVOUCHERID"].ToString();
                            GlobalVariablesCounter.GlobalCreditNoteCount += 1;
                        }


                    }
                }


                return RedeemCreditNo;

            }
            public async Task<string> GetIssueCreditNoteNumber(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                ThrowIf.Null(request, "request");
                string xmlData = string.Empty;
                string transactionId = request.SalesOrder.Id;
                object[] parameterList = new object[]
                        {
                                      transactionId
                        };

                InvokeExtensionMethodRealtimeRequest realtimeRequest = new InvokeExtensionMethodRealtimeRequest(
                                                "IssueGetCreditNote", parameterList);
                InvokeExtensionMethodRealtimeResponse realtimeResponse = await request.RequestContext.ExecuteAsync<InvokeExtensionMethodRealtimeResponse>(realtimeRequest).ConfigureAwait(false);
                ReadOnlyCollection<object> results = realtimeResponse.Result;
                bool success = Convert.ToBoolean(results[0]);
                if (!success)
                {
                    Console.WriteLine("Data not found");
                }
                else
                {

                    xmlData = results[1].ToString();

                }
                return xmlData;
            }
            public async Task<string> GetIssueCreditValidity(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {

                ThrowIf.Null(request, "request");
                string xmlData = string.Empty;
                string formattedDate = string.Empty;
                string transactionId = request.SalesOrder.Id;

                object[] parameterList = new object[]
                        {
                                      transactionId
                        };

                InvokeExtensionMethodRealtimeRequest realtimeRequest = new InvokeExtensionMethodRealtimeRequest(
                                                "getCreditExpiry", parameterList);
                InvokeExtensionMethodRealtimeResponse realtimeResponse = await request.RequestContext.ExecuteAsync<InvokeExtensionMethodRealtimeResponse>(realtimeRequest).ConfigureAwait(false);
                ReadOnlyCollection<object> results = realtimeResponse.Result;
                bool success = Convert.ToBoolean(results[0]);
                if (!success)
                {
                    Console.WriteLine("Data not found");
                }
                else
                {


                    xmlData = results[2].ToString();
                    string[] parts = xmlData.Split(' ');
                    string datePart = parts[0];
                    string[] dateParts = datePart.Split('/');
                    formattedDate = $"{dateParts[1]}/{dateParts[0]}/{dateParts[2]}";


                }
                return formattedDate;
            }
            //public async Task<string> XenoCoupon(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            //{
            //    string CouponCode = string.Empty;
            //    ThrowIf.Null(request, "request");
            //    DataSet dsCouponName = new DataSet();
            //    DataTable dtCouponName = null;
            //    string Query = @"SELECT COUPONCODE FROM EXT.AcxRetailTransactionDiscountTrans WHERE RECEIPTID = @RECEIPTID AND STOREID = @STORENUMBER AND DATAAREAID = @DATAAREAID";
            //    ParameterSet parameterSet = new ParameterSet();
            //    parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
            //    parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
            //    parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

            //    using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
            //    {
            //        dsCouponName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
            //        dtCouponName = dsCouponName.Tables[0];
            //    }
            //    if (dtCouponName != null)
            //    {
            //        if (dtCouponName.Rows.Count > 0)
            //        {
            //            CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
            //            if (string.IsNullOrEmpty(CouponCode))
            //            {
            //                CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
            //            }

            //        }
            //    }
            //    return CouponCode;

            //}
            public async Task<string> XenoCoupon(GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string CouponCode = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCouponName = new DataSet();
                DataTable dtCouponName = null;
                string Query = @"SELECT COUPONCODE FROM EXT.AcxRetailTransactionDiscountTrans WHERE RECEIPTID = @RECEIPTID AND STOREID = @STORENUMBER AND DATAAREAID = @DATAAREAID and ACXSOURCE = 'XENO'";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCouponName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtCouponName = dsCouponName.Tables[0];
                }
                if (dtCouponName != null)
                {
                    if (dtCouponName.Rows.Count > 0)
                    {
                        CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
                        if (string.IsNullOrEmpty(CouponCode))
                        {
                            CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
                        }

                    }
                }
                return CouponCode;

            }
            public async Task<string> ErCoupon(SalesLine salesLine, GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string CouponCode = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsCouponName = new DataSet();
                DataTable dtCouponName = null;
                string Query = @"SELECT COUPONCODE FROM EXT.AcxRetailTransactionDiscountTrans WHERE RECEIPTID = @RECEIPTID AND STOREID = @STORENUMBER AND DATAAREAID = @DATAAREAID and ACXSOURCE = 'ER' and LINENUM = @SALELINENUM";
                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@STORENUMBER"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                parameterSet["@RECEIPTID"] = request.SalesOrder.ReceiptId;
                parameterSet["@DATAAREAID"] = request.RequestContext.GetChannelConfiguration().InventLocationDataAreaId;
                parameterSet["@SALELINENUM"] = salesLine.LineNumber;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsCouponName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtCouponName = dsCouponName.Tables[0];
                }
                if (dtCouponName != null)
                {
                    if (dtCouponName.Rows.Count > 0)
                    {
                        CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
                        if (string.IsNullOrEmpty(CouponCode))
                        {
                            CouponCode = dtCouponName.Rows[0]["COUPONCODE"].ToString();
                        }

                    }
                }
                return CouponCode;

            }
            public async Task<string> GetSalesGroupName(SalesLine salesLine, GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string SalePersonName = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsSalePersonName = new DataSet();
                DataTable dtSalePersonName = null;
                string Query = @"select NAME,GROUPID  from ax.COMMISSIONSALESGROUP where GROUPID =@GROUPID";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@GROUPID"] = salesLine.CommissionSalesGroup;

                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsSalePersonName = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtSalePersonName = dsSalePersonName.Tables[0];
                }
                if (dtSalePersonName != null)
                {
                    if (dtSalePersonName.Rows.Count > 0)
                    {
                        SalePersonName = dtSalePersonName.Rows[0]["NAME"].ToString();
                        if (string.IsNullOrEmpty(SalePersonName))
                        {
                            SalePersonName = dtSalePersonName.Rows[0]["NAME"].ToString();
                        }

                    }
                }
                return SalePersonName;
            }
            public async Task<string> GetActualRedeemAmount(SalesLine salesLine, GetSalesTransactionCustomReceiptFieldServiceRequest request)
            {
                string RefundAmount = string.Empty;
                ThrowIf.Null(request, "request");
                DataSet dsRefundAmount = new DataSet();
                DataTable dtRefundAmount = null;
                string Query = @"select REFUNDABLEAMOUNT from ax.RETAILTRANSACTIONPAYMENTTRANS 
                                 where TRANSACTIONID = @TRANSACTIONID AND STORE =  @STORE
                                 and CREDITVOUCHERID != '' and TENDERTYPE = 12 and REFUNDABLEAMOUNT !=0  AND CHANGELINE = 0";

                ParameterSet parameterSet = new ParameterSet();
                parameterSet["@TRANSACTIONID"] = request.SalesOrder.Id;
                parameterSet["@STORE"] = request.RequestContext.GetDeviceConfiguration().StoreNumber;
                using (DatabaseContext databaseContext = new DatabaseContext(request.RequestContext))
                {
                    dsRefundAmount = await databaseContext.ExecuteQueryDataSetAsync(Query, parameterSet).ConfigureAwait(false);
                    dtRefundAmount = dsRefundAmount.Tables[0];
                }
                if (dtRefundAmount.Rows.Count > 0 && dtRefundAmount != null)
                {
                    RefundAmount = dtRefundAmount != null && dtRefundAmount.Rows.Count > 0 && !string.IsNullOrEmpty(dtRefundAmount.Rows[0]["REFUNDABLEAMOUNT"].ToString())
                    ? Math.Round(Convert.ToDecimal(dtRefundAmount.Rows[0]["REFUNDABLEAMOUNT"]), 2).ToString("F2")
                    : "0.00";
                }
                return RefundAmount;
            }
        }
    }
}
