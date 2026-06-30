using APIGateWay.ModelLayer.ErrorException;
using Microsoft.Extensions.Configuration;
using Sap.Data.Hana;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public class hanaservice
    {
        private readonly IConfiguration _configuration;
        public hanaservice(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<List<T>> ExcuteSpAsync<T>(string StoredProcedure, params HanaParameter[] parameters) where T : class
        {
            // Validate the stored procedure name
            var validProcedureNames = new[] { "GRPOINPUT", "GRPOITEMBYPO", "VALIDATEUSER", "PICKLISTLASTCODE", "GETSOURCEPATH", "GETATTACHMENT", "PICKLISTLOADINGBYDOCENTRY", "GETPICKLISTLOADING", "INVITEMBYBATCH", "INVTTOWAREHOUSE", "INVTINPUT", "INVPWHSQTY", "INVPINPUT", "DISASSEMBLEBATCH", "GETPOSTGRPO", "GRPOLASTCODE", "PAYMENTINPUT", "PRODDATA", "PICKLISTBYITEMCODE", "PICKLISTITEMCODEBYDOCE", "PRODUCTIONNUMTRANSACTION", "DISASSEMBLERECIPTLIST", "PREVIOUS_RETURN_QUANTITY", "GETDISASSEMBLEDATA", "DISASSEMBLERECIPT", "DISASSEMBLEISSUE", "GETPORECEIPT", "PRODINVPINPUT", "PRODISSUE", "PRODRECEIPT", "PICKLISTBYBATCHNUMBER", "GETQRDATA", "GETRETURN", "GETRETURNITEMS", "INVOICEDATAFORCRMEMO", "SALESITEMGROUP", "SALESITEMBYSIZE", "GETSQBYSLPCODE", "GETSOBYSLPCODE", "SAORDERHISTORY", "SAQUOTATIONHISTORY", "GETHISTORYBYSEARCH", "GETDELANDINVBYSEARCH", "GETPICKLISTBYSEARCH", "SADELIVERYDETAILS", "SADELIVERYITEMBYDOC", "SAINVOICE", "SAINVOICETEMBYDOC", "SALESITEMBYBYSEARCH", "GETSALESCUSTOMERDATA", "GETDOCTYPE" };

            if (!validProcedureNames.Contains(StoredProcedure))
            {
                throw new ArgumentException("Invalid stored procedure name", StoredProcedure);
            }

            var result = new List<T>();

            // string connString = "driver={HDBODBC}; UID = SYSTEM; PWD = HEngine@0202; servernode = WGHANA:30013; DATABASENAME = NDB; CurrentSchema = MELWAPROD_APP";

            using (var connection = new HanaConnection(_configuration["ConnectionStrings:DefaultConnection"]))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = StoredProcedure;

                    if (parameters != null)
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.Add(new HanaParameter(parameter.ParameterName, parameter.Value));
                        }
                    }

                    using (HanaDataReader reader = (HanaDataReader)await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = Activator.CreateInstance<T>();

                            foreach (var property in typeof(T).GetProperties())
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                                {
                                    var value = reader[property.Name];

                                    // Handle conversion from HanaDecimal to decimal
                                    if (property.PropertyType == typeof(decimal) && value is Sap.Data.Hana.HanaDecimal)
                                    {
                                        property.SetValue(item, Convert.ToDecimal(value));
                                    }
                                    else
                                    {
                                        property.SetValue(item, value == DBNull.Value ? null : value);
                                    }
                                }
                            }
                            result.Add(item);
                        }
                    }
                }
            }

            if (result == null || result.Count == 0)
            {
                throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
            }

            return result;
        }
    }
}
