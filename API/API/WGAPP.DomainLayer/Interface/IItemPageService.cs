//using WGAPP.ModelLayer;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace WGAPP.DomainLayer.Interface
//{
//    public interface IItemPageService
//    {
//        Task<List<ItemGroupModel>> GetItemGroupName();
//        Task<List<ItemTableModel>> GetItemModels(string grpCode);
//        Task<ItemSearchTable> GetItemsBySearchAndTotal(string searchText, int pageNumber, int pageSize);
//        Task<ItemSearchTable> GetItemsBySearchTable(string searchText, int pageNumber, int pageSize);
//        Task<List<PriceListModal>> GetItemPriceListAsync(string ItemCode);
//        Task<string> InsertOrUpdateCartItemsAsync(List<CartItem> cartItems);
//        Task<string> RemoveDatafromTable();
//        Task<List<CartItem>> GetCartItemsAsync();
//    }
//}
