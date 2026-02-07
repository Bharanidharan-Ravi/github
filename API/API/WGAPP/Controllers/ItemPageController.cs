//using WGAPP.BusinessLayer.Interface;
//using WGAPP.ModelLayer;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Cors;
//using Microsoft.AspNetCore.Mvc;
//using System.Drawing.Printing;

//namespace WGAPP.Controllers
//{

//    [EnableCors("AllowAll")]
//    [ApiController]
//    [Route("api/[controller]")]
//    public class ItemPageController : ControllerBase
//    {
//        private readonly IItemPageRepository _ItemPageRepository;
//        public ItemPageController( IItemPageRepository ItemPageRepository)
//        {
//            _ItemPageRepository = ItemPageRepository;
//        }

//        [HttpGet("GetItemGroup")]
//        public async Task<IActionResult> getuser()
//        {
//            var dbUser = await _ItemPageRepository.GetItemGroup();

//            if (dbUser == null)
//            {
//                return NotFound("No valid user");
//            }
//            return Ok(dbUser);
//        }
//        [HttpGet ("GetItemByGroup")]
//        public async Task<IActionResult> GetItemByGroup(string grpCode)
//        {
//            var dbUser = await _ItemPageRepository.GetItemModels(grpCode);

//            if (dbUser == null)
//            {
//                return NotFound("There is no item group");
//            }
//            return Ok(dbUser);
//        }

//        [HttpGet ("GetItemBySearch")]
//        public async Task<IActionResult> GetItemsBySearchAndTotal(string searchText, int pageNumber, int pageSize)
//        {
//            var Item = await _ItemPageRepository.GetItemsBySearchAndTotal(searchText,pageNumber,pageSize);

//            if (Item == null)
//            {
//                return NotFound("No item is there");
//            }
//            return Ok(Item);
//        }

//        [HttpGet("GetPriceListByItem")]
//        [AllowAnonymous]
//        public async Task<IActionResult> GetItemPriceListAsync(string ItemCode)
//        {
//            var Item = await _ItemPageRepository.GetItemPriceListAsync(ItemCode);

//            if (Item == null)
//            {
//                return NotFound("Price List not avaliable");
//            }
//            return Ok(Item);
//        }
//        [HttpPost("InsertOrUpdateCartItems")]
//        public async Task<string> InsertOrUpdateCartItemsAsync(List<CartItem> cartItems)
//        {
//            var cartItem = await _ItemPageRepository.InsertOrUpdateCartItemsAsync(cartItems);
//            return cartItem;
//        }
//        [HttpPost ("RemoveCartItems")]
//        public async Task<string> RemoveDatafromTable()
//        {
//            var cartItem = await _ItemPageRepository.RemoveDatafromTable();
//            return cartItem;
//        }
//        [HttpGet("GetCartItem")]
//        public async Task<List<CartItem>> GetCartItemsAsync()
//        {
//            var cartItem = await _ItemPageRepository.GetCartItemsAsync();
//            return cartItem;
//        }
//    }   
// }
