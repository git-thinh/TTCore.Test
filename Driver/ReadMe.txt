
var config = Configuration.Default.WithDefaultLoader();
var address = "https://en.wikipedia.org/wiki/List_of_The_Big_Bang_Theory_episodes";
var context = AngleSharp.BrowsingContext.New(config);
var document = await context.OpenAsync(address);
var cellSelector = "tr.vevent td:nth-child(3)";
var cells = document.QuerySelectorAll(cellSelector);
var titles = cells.Select(m => m.TextContent);

var url = "http://anglesharp.azurewebsites.net/PostUrlencodeNormal";
var config = new Configuration { AllowRequests = true };
var html = AngleSharp.DocumentBuilder.Html(new Uri(url), config);
var form = html.Forms[0] as IHtmlFormElement;
var name = form.Elements["Name"] as IHtmlInputElement;
var number = form.Elements["Number"] as IHtmlInputElement;
var active = form.Elements["IsActive"] as IHtmlInputElement;
name.Value = "Test";
number.Value = "1";
active.IsChecked = true;
form.Submit();