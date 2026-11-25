using System.Globalization;
using System.Threading;
using POGLib.Shops.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    public class SlotInfoTests
    {
        [Fact]
        public void RoundTripKeepsEveryField()
        {
            var a = new SlotInfo(174, "ShopDemo:berry_stand", new[] { 0.0064f, 0.698f, 0.2404f }, new[] { 53.5979f, 184.1979f, 4.785f }, true);
            Assert.True(SlotInfo.TryDecode(a.Encode(), out var b));
            Assert.Equal(174ul, b.NetworkId);
            Assert.Equal("ShopDemo:berry_stand", b.SlotId);
            Assert.Equal(a.TagPos, b.TagPos);
            Assert.Equal(a.TagRot, b.TagRot);
            Assert.True(b.TagHidden);
        }

        [Fact]
        public void AMissingTagPoseStaysMissing()
        {
            var a = new SlotInfo(5, "Mod:slot", null, null, false);
            Assert.Equal("1|5|0|-|-|Mod:slot", a.Encode());
            Assert.True(SlotInfo.TryDecode(a.Encode(), out var b));
            Assert.Null(b.TagPos); Assert.Null(b.TagRot); Assert.False(b.TagHidden);
        }

        [Fact]
        public void ABarInTheSlotIdDoesNotBreakTheFields()
        {
            var a = new SlotInfo(9, "Mod:odd|name", new[] { 1f, 2f, 3f }, null, false);
            Assert.True(SlotInfo.TryDecode(a.Encode(), out var b));
            Assert.Equal("Mod:odd|name", b.SlotId);
            Assert.Equal(new[] { 1f, 2f, 3f }, b.TagPos);
        }

        [Fact]
        public void NumbersDoNotDependOnTheMachinesCulture()
        {
            var saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("uk-UA"); // decimal comma
                string s = new SlotInfo(1, "M:s", new[] { 0.5f, -1.25f, 3f }, null, false).Encode();
                Assert.Equal("1|1|0|0.5,-1.25,3|-|M:s", s);
                Assert.True(SlotInfo.TryDecode(s, out var b));
                Assert.Equal(new[] { 0.5f, -1.25f, 3f }, b.TagPos);
            }
            finally { Thread.CurrentThread.CurrentCulture = saved; }
        }

        [Fact]
        public void AVectorThatIsNotThreeFiniteNumbersIsLeftOut() =>
            Assert.Null(new SlotInfo(1, "M:s", new[] { 1f, float.NaN, 0f }, null, false).TagPos);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2|1|0|-|-|M:s")]
        [InlineData("1|x|0|-|-|M:s")]
        [InlineData("1|1|2|-|-|M:s")]
        [InlineData("1|1|0|1,2|-|M:s")]
        [InlineData("1|1|0|-|a,b,c|M:s")]
        [InlineData("1|1|0|NaN,0,0|-|M:s")]
        [InlineData("1|1|0|-|-|")]
        [InlineData("1|1|0|-|-")]
        public void SomethingElseIsRefused(string text) => Assert.False(SlotInfo.TryDecode(text, out _));

        [Fact]
        public void TheQuestionIsTheNetworkId()
        {
            Assert.True(SlotInfo.TryReadAsk(SlotInfo.Ask(18446744073709551615ul), out var id));
            Assert.Equal(ulong.MaxValue, id);
            Assert.False(SlotInfo.TryReadAsk("-1", out _));
            Assert.False(SlotInfo.TryReadAsk("", out _));
        }
    }

    public class SellerInfoTests
    {
        [Theory]
        [InlineData("Nobody")]
        [InlineData("A | B")]
        [InlineData("Дядько №7")]
        public void RoundTripKeepsEveryField(string name)
        {
            Assert.True(SellerInfo.TryDecode(new SellerInfo(174, "POGBlackMarket:mask", name).Encode(), out var b));
            Assert.Equal((174ul, "POGBlackMarket:mask", name), (b.NetworkId, b.FullId, b.Name));
        }

        [Fact]
        public void ABarInTheIdDoesNotReachTheName()
        {
            Assert.True(SellerInfo.TryDecode(new SellerInfo(1, "M:a|b", "N").Encode(), out var b));
            Assert.Equal(("M:a_b", "N"), (b.FullId, b.Name));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2|1|M:a|N")]
        [InlineData("1|x|M:a|N")]
        [InlineData("1|-1|M:a|N")]
        [InlineData("1|1||N")]
        [InlineData("1|1|M:a|")]
        [InlineData("1|1|M:a")]
        [InlineData("1|1|M:a|xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
        public void AnythingElseIsRefused(string text) => Assert.False(SellerInfo.TryDecode(text, out _));
    }
}
