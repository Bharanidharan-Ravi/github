//using WGAPP.BusinessLayer.Interface;
//using WGAPP.DomainLayer.Interface;
//using WGAPP.ModelLayer;
//using Microsoft.Extensions.Configuration;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace WGAPP.BusinessLayer.Repository
//{
//    public class ItemPageRepository : IItemPageRepository
//    {
//        private readonly IItemPageService _itemPageService;


//        public ItemPageRepository(IItemPageService itemPageService)
//        {
//            _itemPageService= itemPageService;
//        }

//        public async Task<List<ItemGroupModel>> GetItemGroup()
//        {
//            var itemgrp = await _itemPageService.GetItemGroupName();
//            return itemgrp;
//        }
//        public async Task<List<ItemTableModel>> GetItemModels(string grpCode)
//        {
//            var itemgrp = await _itemPageService.GetItemModels(grpCode);
//            return itemgrp;
//        }
//        public async Task<ItemSearchTable> GetItemsBySearchAndTotal(string searchText, int pageNumber, int pageSize)
//        {
//            var item =await _itemPageService.GetItemsBySearchAndTotal(searchText, pageNumber, pageSize);
//            return item;    
//        }
//        public async Task<List<PriceListModal>> GetItemPriceListAsync(string ItemCode)
//        {
//            var price = await _itemPageService.GetItemPriceListAsync(ItemCode);   
//            return price;
//        }
//       public async Task<string> InsertOrUpdateCartItemsAsync(List<CartItem> cartItems)
//        {
//            var cartItem = await _itemPageService.InsertOrUpdateCartItemsAsync(cartItems);
//            return cartItem;
//        }
//        public async Task<string> RemoveDatafromTable()
//        {
//            var cartItem = await _itemPageService.RemoveDatafromTable();
//            return cartItem;
//        }
//        public async Task<List<CartItem>> GetCartItemsAsync()
//        {
//            var cartItem = await _itemPageService.GetCartItemsAsync();
//            return cartItem;
//        }
//    }
//}