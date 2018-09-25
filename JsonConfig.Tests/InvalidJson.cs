using System;
using NUnit.Framework;

namespace JsonConfig.Tests
{
	[TestFixture()]
	public class InvalidJson
	{
		[Test]
		public void EvidentlyInvalidJson ()
		{
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => {
                dynamic scope = Config.Global;
                scope.ApplyJson("jibberisch");
            });
		}
		[Test]
		public void MissingObjectIdentifier()
		{
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => {
                dynamic scope = Config.Global;
                var invalid_json = @" { [1, 2, 3] }";
                scope.ApplyJson(invalid_json);
            });
		}
	}
}

