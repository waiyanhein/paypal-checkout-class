using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using thailandshop.Models.ViewModels.CheckOut;
using thailandshop.Models.Abstract;
using System.Net;
using thailandshop.Models.Paypal;
using System.Collections.Specialized;

namespace thailandshop.Controllers
{
    public class CheckOutController : Controller
    {
        private IShoppingCart cart;

        private IOrderProcessor OrderProcesor;

        private IContentRepository contentRepo;

        public CheckOutController(IShoppingCart cartParam,IOrderProcessor orderParam,IContentRepository contentRepoParam)
        {
            this.cart = cartParam;

            this.OrderProcesor = orderParam;

            this.contentRepo = contentRepoParam;
        }

        public ActionResult OrderForm()
        {
            var items = cart.Items();
            if(items==null || items.Count()<1)
            {
                return RedirectToAction("Categories", "Catalog", new { area = "" ,checkoutmessage = true});
            }
            OrderFormViewModel model = new OrderFormViewModel
            {
                PaymentOptions = this.paymentOptionsItems()
            };
            return View(model);
        }

        [HttpPost]
        public ActionResult OrderForm(OrderFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                Session["OrderFormModel"] = model;
                //online payment
                if (model.Payment == "online")
                {
                    PayPalRedirect redirect = this.SetExpressCheckout();
                    return new RedirectResult(redirect.Url);
                }
                else//cash on delivery
                {
                    return RedirectToAction("ConfirmOrder");
                }
            }
            model.PaymentOptions = this.paymentOptionsItems();
            return View(model);
        }

        public ActionResult ConfirmOrder()
        {
            if (Session["OrderFormModel"]==null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var items = cart.Items();
            if(items==null || items.Count()<1)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            OrderFormViewModel order_form = (OrderFormViewModel)Session["OrderFormModel"];
            ConfirmOrderViewModel model = new ConfirmOrderViewModel
            {
                Name = order_form.Name,
                Email = order_form.Email,
                Phone = order_form.Code + " - " + order_form.Phone,
                Street = order_form.Street,
                HomeNo = order_form.HomeNo,
                TownShip = order_form.TownShip,
                City = order_form.City,
                Payment = order_form.Payment,
                Order_line = items
            };
            //if payment is online - redirected from paypal after paypal login
            if (order_form.Payment == "online")
            {
                if (string.IsNullOrEmpty(Request["token"]) || string.IsNullOrEmpty(Request["PayerID"]))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                model.PayerID = Request["PayerID"];
                model.Token = Request["token"];
            }
            return View(model);
        }

        //Cash on devlivery payment submit
        public ActionResult SubmitCODOrder()
        {
            if (Session["OrderFormModel"] == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            OrderFormViewModel order_form = (OrderFormViewModel)Session["OrderFormModel"];

            bool success = OrderProcesor.GenerateOrder(order_form.Name, order_form.Email, order_form.Code + "-" + order_form.Phone, 
                order_form.Street, order_form.HomeNo, order_form.TownShip, order_form.City, false,contentRepo.GetCurrencyRate());

            if(success)
            {
                Session.RemoveAll();
                cart.ClearCart();
                return RedirectToAction("ThankYou");
            }
            return RedirectToAction("Error");//show error page
        }

        //Paypal - make payment - do express checkout payment
        [HttpPost]
        public ActionResult SubmitOrder(string PayerID,string Token)
        {
            if (string.IsNullOrEmpty(PayerID) || string.IsNullOrEmpty(Token) || Session["OrderFormModel"]==null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var items = cart.Items();
            decimal total_amt = items.Sum(x => x.Saleprice * x.Qty);
            string total = String.Format("{0:0.00#}", total_amt);

            NameValueCollection values = Paypal.DoExpressCheckout(Token, PayerID, total);

            if (values != null && values["ACK"] != null && values["ACK"] == "Success")
            {
                //save order
                OrderFormViewModel order_form = (OrderFormViewModel)Session["OrderFormModel"];

                bool success = OrderProcesor.GenerateOrder(order_form.Name, order_form.Email, order_form.Code + "-" + order_form.Phone,
                    order_form.Street, order_form.HomeNo, order_form.TownShip, order_form.City, true,contentRepo.GetCurrencyRate());

                if (success)
                {
                    Session.RemoveAll();
                    cart.ClearCart();
                }
                return RedirectToAction("ThankYou");
            }
            return View("Error");//Order error page
        }

        private PayPalRedirect SetExpressCheckout()
        {
            var items = cart.Items();
            NameValueCollection order_line = new NameValueCollection();

            order_line = Paypal.GeneratePaypalOrderLine(items);

            decimal total_amt = items.Sum(x => x.Saleprice * x.Qty);
            string total = String.Format("{0:0.00#}",total_amt);
            PayPalRedirect redirect = Paypal.ExpressCheckout(order_line, total);//Paypal.ExpressCheckout(new PayPalOrder { Amount = 50 });  
            return redirect;
        }

        public ActionResult ThankYou()
        {
            return View();
        }

        private IEnumerable<SelectListItem> paymentOptionsItems()
        {
            List<SelectListItem> items = new List<SelectListItem>();

            SelectListItem online = new SelectListItem
            {
                Value = "online",
                Text = "Paypal(online)",
                Selected = true
            };

            SelectListItem cod = new SelectListItem
            {
                Value = "cash",
                Text = "Cash on delivery"
            };

            items.Add(online);
            items.Add(cod);

            return items;
        }
    }
}
