//using WGAPP.ModelLayer;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace WGAPP.BusinessLayer.Interface
//{
//    public interface IItemPageRepository
//    {
//        Task<List<ItemGroupModel>> GetItemGroup();
//        Task<List<ItemTableModel>> GetItemModels(string grpCode);
//        Task<ItemSearchTable> GetItemsBySearchAndTotal(string searchText, int pageNumber, int pageSize);
//        Task<List<PriceListModal>> GetItemPriceListAsync(string ItemCode);
//        Task<string> InsertOrUpdateCartItemsAsync(List<CartItem> cartItems);
//        Task<string> RemoveDatafromTable();
//        Task<List<CartItem>> GetCartItemsAsync();
//    }
//}
