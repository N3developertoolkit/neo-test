using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace build_tasks
{
    public class TestTasks
    {
        [Fact]
        public void TestName()
        {

            var foo = new Neo.BuildTasks.NeoExpressBatch();

            var engine = Moq.Mock.Of<IBuildEngine6>();
            foo.BuildEngine = engine;
            foo.Execute();


            // foo.


            // Given
        
            // When
        
            // Then
        }
    }

}
