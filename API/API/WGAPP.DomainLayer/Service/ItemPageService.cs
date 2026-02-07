//using WGAPP.DomainLayer.DBContext;
//using WGAPP.DomainLayer.Interface;
//using WGAPP.DomainLayer.Service.CommonService;
//using WGAPP.ModelLayer;
//using Microsoft.Data.SqlClient;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace WGAPP.DomainLayer.Service
//{
//    public class ItemPageService : IItemPageService
//    {

//        private readonly DealerCommonService _commonService;
//        private readonly WGAPPDbContext _DbContext;

//        public ItemPageService(DealerCommonService commonService, WGAPPDbContext WGAPPDbContext)
//        {
//            _commonService = commonService;
//            _DbContext = WGAPPDbContext;
//        }
//        public async Task<List<ItemGroupModel>> GetItemGroupName()
//        {
//            var parameters = new SqlParameter("@DatabaseName", _commonService.databaseName);
//            var itemgroup = await _commonService.ExecuteGetItemAsyc<ItemGroupModel>("GetItemGroup", parameters);
//            return itemgroup;
//        }

//        public async Task<List<ItemTableModel>> GetItemModels(string grpCode)
//        {
//            var parameters = new SqlParameter[]
//            {
//                new SqlParameter("@DatabaseName", _commonService.databaseName),
//                new SqlParameter("@grpCode", grpCode)
//            };
//            var itemgroup = await _commonService.ExecuteGetItemAsyc<ItemTableModel>("GetItem", parameters);
//            return itemgroup;
//        }

//        public async Task<ItemSearchTable> GetItemsBySearchAndTotal(string searchText, int pageNumber, int pageSize)
//        {
//            try
//            {
//                var parameters = new SqlParameter[]
//                {
//            new SqlParameter("@DatabaseName", _commonService.databaseName),
//            new SqlParameter("@searchText", searchText ?? string.Empty),
//            new SqlParameter("@PageNumber", pageNumber),
//            new SqlParameter("@PageSize", pageSize),
//                };

//                var connectionString = _DbContext.Database.GetConnectionString();
//                using (var connection = new SqlConnection(connectionString))
//                {
//                    await connection.OpenAsync();
//                    using (var command = new SqlCommand("getItemBySearchAndTotal", connection))
//                    {
//                        command.CommandType = CommandType.StoredProcedure;
//                        command.Parameters.AddRange(parameters);

//                        using (var reader = await command.ExecuteReaderAsync())
//                        {
//                            // Read the first result set (total count)
//                            var totalCount = 0;
//                            if (await reader.ReadAsync())
//                            {
//                                if (!reader.IsDBNull(0))
//                                {
//                                    var totalCountString = reader.GetString(0);  // Ensure the column index matches the stored procedure's output
//                                    int.TryParse(totalCountString, out totalCount);
//                                }
//                            }

//                            // Move to the second result set (items)
//                            await reader.NextResultAsync();

//                            var items = new List<ItemTableModel>();
//                            while (await reader.ReadAsync())
//                            {
//                                var item = new ItemTableModel
//                                {
//                                    Item_Code = reader.IsDBNull(reader.GetOrdinal("Item_Code")) ? string.Empty : reader.GetString(reader.GetOrdinal("Item_Code")),
//                                    Item_Name = reader.IsDBNull(reader.GetOrdinal("Item_Name")) ? string.Empty : reader.GetString(reader.GetOrdinal("Item_Name")),
//                                    Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? string.Empty : reader.GetString(reader.GetOrdinal("Quantity")),
//                                    UOM = reader.IsDBNull(reader.GetOrdinal("UOM")) ? string.Empty : reader.GetString(reader.GetOrdinal("UOM")),
//                                    Uom_multiple = reader.IsDBNull(reader.GetOrdinal("Uom_multiple")) ? string.Empty : reader.GetString(reader.GetOrdinal("Uom_multiple")),
//                                    Price = reader.IsDBNull(reader.GetOrdinal("Price")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Price")),
//                                    Discount_Percentage = reader.IsDBNull(reader.GetOrdinal("Discount_Percentage")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Discount_Percentage")),
//                                    Discount_Price = reader.IsDBNull(reader.GetOrdinal("Discount_Price")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Discount_Price")),
//                                    Total = reader.IsDBNull(reader.GetOrdinal("Total")) ? string.Empty : reader.GetString(reader.GetOrdinal("Total")),
//                                };
//                                items.Add(item);
//                            }

//                            // Return the combined result
//                            return new ItemSearchTable
//                            {
//                                TotalCount = totalCount,
//                                Items = items
//                            };
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                // Log the exception
//                Console.Error.WriteLine($"An error occurred: {ex.Message}");
//                // Handle or rethrow the exception as needed
//                throw;
//            }
//        }

//          public async Task<ItemSearchTable> GetItemsBySearchTable(string searchText, int pageNumber, int pageSize)
//        {
//            try
//            {
//                var parameters = new SqlParameter[]
//                {
//            new SqlParameter("@DatabaseName", _commonService.databaseName),
//            new SqlParameter("@searchText", searchText ?? string.Empty),
//            new SqlParameter("@PageNumber", pageNumber),
//            new SqlParameter("@PageSize", pageSize),
//                };

//                var connectionString = _DbContext.Database.GetConnectionString();
//                using (var connection = new SqlConnection(connectionString))
//                {
//                    await connection.OpenAsync();
//                    using (var command = new SqlCommand("getItemBySearchTable", connection))
//                    {
//                        command.CommandType = CommandType.StoredProcedure;
//                        command.Parameters.AddRange(parameters);

//                        using (var reader = await command.ExecuteReaderAsync())
//                        {
//                            // Read the first result set (total count)
//                            var totalCount = 0;
//                            if (await reader.ReadAsync())
//                            {
//                                if (!reader.IsDBNull(0))
//                                {
//                                    var totalCountString = reader.GetString(0);  // Ensure the column index matches the stored procedure's output
//                                    int.TryParse(totalCountString, out totalCount);
//                                }
//                            }

//                            // Move to the second result set (items)
//                            await reader.NextResultAsync();

//                            var items = new List<ItemTableModel>();
//                            while (await reader.ReadAsync())
//                            {
//                                var item = new ItemTableModel
//                                {
//                                    Item_Code = reader.IsDBNull(reader.GetOrdinal("Item_Code")) ? string.Empty : reader.GetString(reader.GetOrdinal("Item_Code")),
//                                    Item_Name = reader.IsDBNull(reader.GetOrdinal("Item_Name")) ? string.Empty : reader.GetString(reader.GetOrdinal("Item_Name")),
//                                    Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? string.Empty : reader.GetString(reader.GetOrdinal("Quantity")),
//                                    UOM = reader.IsDBNull(reader.GetOrdinal("UOM")) ? string.Empty : reader.GetString(reader.GetOrdinal("UOM")),
//                                    Uom_multiple = reader.IsDBNull(reader.GetOrdinal("Uom_multiple")) ? string.Empty : reader.GetString(reader.GetOrdinal("Uom_multiple")),
//                                    Price = reader.IsDBNull(reader.GetOrdinal("Price")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Price")),
//                                    Discount_Percentage = reader.IsDBNull(reader.GetOrdinal("Discount_Percentage")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Discount_Percentage")),
//                                    Discount_Price = reader.IsDBNull(reader.GetOrdinal("Discount_Price")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("Discount_Price")),
//                                    Total = reader.IsDBNull(reader.GetOrdinal("Total")) ? string.Empty : reader.GetString(reader.GetOrdinal("Total")),
//                                };
//                                items.Add(item);
//                            }

//                            // Return the combined result
//                            return new ItemSearchTable
//                            {
//                                TotalCount = totalCount,
//                                Items = items
//                            };
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                // Log the exception
//                Console.Error.WriteLine($"An error occurred: {ex.Message}");
//                // Handle or rethrow the exception as needed
//                throw;
//            }
//        }
//        public async Task<List<PriceListModal>> GetItemPriceListAsync(string ItemCode)
//        {
//            var parameters = new[]
//            {
//                new SqlParameter("@ItemCode", ItemCode),
//                new SqlParameter("@DatabaseName", _commonService.databaseName)
//            };
//            var priceLists = await _commonService.ExecuteGetItemAsyc<PriceListModal>("GetItemPriceList", parameters);

//            return priceLists;
//        }

//        #region posting a cart items
//        public async Task<string> InsertOrUpdateCartItemsAsync(List<CartItem> cartItems)
//        {
//            string responseMessage = string.Empty;
//            int userId = int.Parse(_commonService.userId.ToString());
//            // Get the existing items for the user
//            var existingItems = _DbContext.CartItems.Where(i => i.UserId == userId);

//            try {
//                _DbContext.CartItems.RemoveRange(existingItems);

//                // Add the new items
//                foreach (var item in cartItems)
//                {
//                    // If salUnitMsr is empty, set it to "0"
//                    if (string.IsNullOrEmpty(item.salUnitMsr))
//                    {
//                        item.salUnitMsr = "0";
//                    }

//                    _DbContext.CartItems.Add(item);
//                }

//                // Save changes to the database
//                await _DbContext.SaveChangesAsync();
//                responseMessage = ApplicationConstants.success_message;
//            } catch (Exception ex) { 
//                responseMessage = ex.Message;
//            }
//            // Delete the existing items

//            return responseMessage; 
//        }
//        #endregion
//        public async Task<string> RemoveDatafromTable()
//        {
//            string responseMessage = string.Empty;
//            int userId = int.Parse(_commonService.userId.ToString());
//            try
//            {
//                var existingItems = _DbContext.CartItems.Where(i => i.UserId == userId);

//                // Delete the existing items
//                _DbContext.CartItems.RemoveRange(existingItems);
//                // Save changes to the database
//                await _DbContext.SaveChangesAsync();
//                responseMessage = ApplicationConstants.success_message;
//            } catch (Exception ex) {
//                responseMessage = ex.Message; 
//            }    
//            return responseMessage;
//        }
//        public async Task<List<CartItem>> GetCartItemsAsync()
//        {
//            int userId = int.Parse(_commonService.userId.ToString());
//            // Get the items for the user
//            var items = await _DbContext.CartItems.Where(i => i.UserId == userId).ToListAsync();

//            // Return the items
//            return items;
//        }
//    }
//}