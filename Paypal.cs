using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using System.Globalization;
using System.Configuration;
using thailandshop.Models.EF;

namespace thailandshop.Models.Paypal
{
    public class Paypal
    {
        public static PayPalRedirect ExpressCheckout(NameValueCollection order_line,string total)
        {
            NameValueCollection values = new NameValueCollection();
            values["METHOD"] = "SetExpressCheckout";
            values["RETURNURL"] = PayPalSettings.ReturnUrl;
            values["CANCELURL"] = PayPalSettings.CancelUrl;
            values["PAYMENTACTION"] = "Sale";
            values["CURRENCY"] = "USD";
            values["USER"] = PayPalSettings.Username;
            values["PWD"] = PayPalSettings.Password;
            values["SIGNATURE"] = PayPalSettings.Signature;
            values["VERSION"] = "95";
            values.Add(order_line);
            values["PAYMENTREQUEST_0_AMT"] = total;//order.Amount.ToString(CultureInfo.InvariantCulture);

            values = Submit(values);

            string ack = values["ACK"].ToLower();

            if (ack == "success" || ack == "successwithwarning")
            {
                return new PayPalRedirect
                {
                    Token = values["TOKEN"],
                    Url = String.Format("https://{0}/cgi-bin/webscr?cmd=_express-checkout&token={1}",
                       PayPalSettings.CgiDomain, values["TOKEN"])
                };
            }
            else
            {
                throw new Exception(values["L_LONGMESSAGE0"]);
            }
        }

        public static NameValueCollection GeneratePaypalOrderLine(IEnumerable<Cart> items)
        {
            NameValueCollection order_line = new NameValueCollection();

            int count = 0;
            foreach (var item in items)
            {
                order_line["L_PAYMENTREQUEST_0_NAME" + count.ToString()] = item.Product.Name;
                order_line["L_PAYMENTREQUEST_0_NUMBER" + count.ToString()] = item.ProductId.ToString();
                order_line["L_PAYMENTREQUEST_0_DESC" + count.ToString()] = item.Product.Description;
                order_line["L_PAYMENTREQUEST_0_QTY" + count.ToString()] = item.Qty.ToString();
                order_line["L_PAYMENTREQUEST_0_AMT" + count.ToString()] = String.Format("{0:0.00#}", item.Saleprice);
                count++;
            }
            return order_line;
        }

        public static NameValueCollection DoExpressCheckout(string token,string payerid,string amount)
        {
            NameValueCollection values = new NameValueCollection();
            values["METHOD"] = "DoExpressCheckoutPayment";
            values["VERSION"] = "95";
            values["USER"] = PayPalSettings.Username;
            values["PWD"] = PayPalSettings.Password;
            values["SIGNATURE"] = PayPalSettings.Signature;
            values["TOKEN"] = token;
            values["PAYERID"] = payerid;
            values["PAYMENTREQUEST_0_PAYMENTACTION"] = "Sale";
            values["PAYMENTREQUEST_0_AMT"] = amount;

            return Submit(values);
        }

        private static NameValueCollection Submit(NameValueCollection values)
        {
            string data = String.Join("&", values.Cast<string>()
              .Select(key => String.Format("{0}={1}", key, HttpUtility.UrlEncode(values[key]))));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
               String.Format("https://{0}/nvp", PayPalSettings.ApiDomain));

            request.Method = "POST";
            request.ContentLength = data.Length;

            using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(data);
            }

            using (StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream()))
            {
                return HttpUtility.ParseQueryString(reader.ReadToEnd());
            }
        }
    }

    public class PayPalOrder
    {
        public decimal Amount { get; set; }
    }

    public class PayPalRedirect
    {
        public string Url { get; set; }
        public string Token { get; set; }
    }

    public static class PayPalSettings
    {
        public static string ApiDomain
        {
            get
            {
                return ShopConfig.PaypalUrl;
                   //: "api-3t.paypal.com";
            }
        }

        public static string CgiDomain
        {
            get
            {
                return ShopConfig.PaypalCgiDomain;
            }
        }

        public static string Signature
        {
            get
            {
                return ShopConfig.PaypalSignature;
            }
        }

        public static string Username
        {
            get
            {
                return ShopConfig.PaypalUserName;
            }
        }

        public static string Password
        {
            get
            {
                return ShopConfig.PaypalPassword;
            }
        }

        public static string ReturnUrl
        {
            get
            {
                return ShopConfig.PaypalReturnUrl;
            }
        }

        public static string CancelUrl
        {
            get
            {
                return ShopConfig.PaypalReturnUrl;
            }
        }
    }
}
