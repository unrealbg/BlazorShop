namespace BlazorShop.Web.Pages.Public
{
    public partial class FAQ
    {
        private List<FaqItem> FAQs = new()
                                         {
                                             new FaqItem
                                                 {
                                                     Question = "How can I track my order?",
                                                     Answer =
                                                         "You can track your order in the 'Orders' section after logging into your account."
                                                 },
                                             new FaqItem
                                                 {
                                                     Question = "What is the return policy?",
                                                     Answer =
                                                         "We offer a 30-day return policy for most products. Please check our 'Returns' section for more details."
                                                 },
                                             new FaqItem
                                                 {
                                                     Question = "How can I contact customer service?",
                                                     Answer =
                                                         "You can reach us at support@yourstore.com or call us at +1 123-456-7890."
                                                 },
                                             new FaqItem
                                                 {
                                                     Question = "What payment methods do you accept?",
                                                     Answer =
                                                         "We accept all major credit cards, PayPal, and bank transfers."
                                                 },
                                             new FaqItem
                                                 {
                                                     Question = "How long does shipping take?",
                                                     Answer =
                                                         "Shipping usually takes 5-7 business days for domestic orders. International shipping times vary."
                                                 }
                                         };

        private class FaqItem
        {
            public required string Question { get; set; }

            public required string Answer { get; set; }
        }
    }
}