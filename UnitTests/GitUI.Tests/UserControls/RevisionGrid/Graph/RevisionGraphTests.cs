﻿using FluentAssertions;
using GitCommands;
using GitUI.UserControls.RevisionGrid.Graph;
using GitUIPluginInterfaces;

namespace GitUITests.UserControls.RevisionGrid
{
    [TestFixture]
    public class RevisionGraphTests
    {
        private RevisionGraph _revisionGraph;

        public void Setup(bool mergeGraphLanesHavingCommonParent, bool finishLoading = false, IEnumerable<GitRevision>? revisions = null)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            _revisionGraph = new RevisionGraph();

            foreach (GitRevision revision in revisions ?? Revisions)
            {
                // Mark the first revision as the current checkout
                if (_revisionGraph.Count == 0)
                {
                    _revisionGraph.HeadId = revision.ObjectId;
                }

                _revisionGraph.Add(revision);
            }

            if (finishLoading)
            {
                _revisionGraph.LoadingCompleted();
                int lastRowIndex = _revisionGraph.Count - 1;
                _revisionGraph.CacheTo(lastRowIndex, lastRowIndex);
            }
        }

        [Test]
        public void ShouldBeAbleToCacheGraphTo([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            Assert.AreEqual(0, _revisionGraph.GetCachedCount());
            _revisionGraph.CacheTo(4, 2);
            Assert.AreEqual(mergeGraphLanesHavingCommonParent ? 3 : 0, _revisionGraph.GetCachedCount());
            _revisionGraph.CacheTo(4, 4);
            Assert.AreEqual(mergeGraphLanesHavingCommonParent ? 5 : 0, _revisionGraph.GetCachedCount());
            _revisionGraph.CacheTo(400, 400);
            Assert.AreEqual(mergeGraphLanesHavingCommonParent ? 6 : 0, _revisionGraph.GetCachedCount());
            _revisionGraph.LoadingCompleted();
            Assert.AreEqual(mergeGraphLanesHavingCommonParent ? 6 + LookAhead : 0, _revisionGraph.GetCachedCount());
            _revisionGraph.CacheTo(400, 400);
            Assert.AreEqual(6 + LookAhead, _revisionGraph.GetCachedCount());
        }

        [Test]
        public void ShouldBeAbleToClear([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            Assert.AreEqual(6 + LookAhead, _revisionGraph.Count);
            _revisionGraph.Clear();
            Assert.AreEqual(0, _revisionGraph.Count);
        }

        [Test]
        public void ShouldBeAbleToHighlightBranch([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);
            Assert.IsTrue(_revisionGraph.GetNodeForRow(0).IsRelative);
            Assert.IsTrue(_revisionGraph.GetNodeForRow(1).IsRelative);
            Assert.IsTrue(_revisionGraph.GetNodeForRow(4).IsRelative);
            _revisionGraph.HighlightBranch(_revisionGraph.GetNodeForRow(1).Objectid);
            Assert.IsFalse(_revisionGraph.GetNodeForRow(0).IsRelative);
            Assert.IsTrue(_revisionGraph.GetNodeForRow(1).IsRelative);
            Assert.IsTrue(_revisionGraph.GetNodeForRow(4).IsRelative);
        }

        [Test]
        public void ShouldBeAbleToGetLaneCount([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            if (!mergeGraphLanesHavingCommonParent)
            {
                _revisionGraph.LoadingCompleted();
            }

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);
            Assert.AreEqual(1, _revisionGraph.GetSegmentsForRow(0).GetLaneCount());
            Assert.AreEqual(1, _revisionGraph.GetSegmentsForRow(1).GetLaneCount());
            Assert.AreEqual(2, _revisionGraph.GetSegmentsForRow(2).GetLaneCount());
            Assert.AreEqual(2, _revisionGraph.GetSegmentsForRow(3).GetLaneCount());
            Assert.AreEqual(1, _revisionGraph.GetSegmentsForRow(4).GetLaneCount());
            Assert.AreEqual(1, _revisionGraph.GetSegmentsForRow(5).GetLaneCount());
        }

        [Test]
        public void ShouldReorderInTopoOrder([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);
            Assert.IsTrue(_revisionGraph.GetTestAccessor().ValidateTopoOrder());

            GitRevision commit1 = new(ObjectId.Random());

            GitRevision commit2 = new(ObjectId.Random());
            commit1.ParentIds = new ObjectId[] { commit2.ObjectId };
            commit2.ParentIds = new ObjectId[] { _revisionGraph.GetNodeForRow(4).Objectid };

            _revisionGraph.Add(commit2); // This commit is now dangling

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);
            Assert.IsTrue(_revisionGraph.GetTestAccessor().ValidateTopoOrder());

            _revisionGraph.Add(commit1); // Add the connecting commit

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);
            Assert.IsTrue(_revisionGraph.GetTestAccessor().ValidateTopoOrder());

            // Add a new head
            GitRevision newHead = new(ObjectId.Random());
            newHead.ParentIds = new ObjectId[] { _revisionGraph.GetNodeForRow(0).Objectid };
            _revisionGraph.Add(newHead); // Add commit that has the current top node as parent.

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count); // Call to cache fix the order
            Assert.IsTrue(_revisionGraph.GetTestAccessor().ValidateTopoOrder());
        }

        [Test] // https://github.com/gitextensions/gitextensions/issues/6193
        public void CacheEmptyGraph([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            _revisionGraph.Clear();
            _revisionGraph.CacheTo(100, 100);
            _revisionGraph.CacheTo(100, 100);
        }

        [Test] // https://github.com/gitextensions/gitextensions/issues/6210
        public async Task DetachedSingleRevision([Values] bool mergeGraphLanesHavingCommonParent)
        {
            Setup(mergeGraphLanesHavingCommonParent);

            _revisionGraph.Clear();

            /* Visualization of commit graph:
             *     Commit1
             *        |
             *        |       Commit2 (detached)
             *        |
             *     Commit3
             */

            GitRevision commit1 = new(ObjectId.Random());

            GitRevision commit2 = new(ObjectId.Random());

            GitRevision commit3 = new(ObjectId.Random());
            commit1.ParentIds = new ObjectId[] { commit3.ObjectId };

            _revisionGraph.Add(commit1);
            _revisionGraph.Add(commit2);
            _revisionGraph.Add(commit3);
            _revisionGraph.LoadingCompleted();

            _revisionGraph.CacheTo(_revisionGraph.Count, _revisionGraph.Count);

            Assert.AreEqual(1, _revisionGraph.GetSegmentsForRow(1).GetCurrentRevisionLane());

            await VerifyGraphLayoutAsync(_revisionGraph);
        }

        [Test]
        public async Task SegmentsAreStraightened([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  4:1,3  5:4  6:5  7:5,6  8:7,2 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsWithCommitsAreStraightened([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  4:1,3  5:2  6:5  7:4  8:4,7  9:8,6 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsWithOutgoingSecondaryMergesAreNotStraightened([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  4:1,3  5:2  6:4,5  7:6  8:6,7  9:8,5 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsWithIncomingMergesAreStraightened([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  4:1,3  5:2,4  6:4  7:4,6  8:7,5 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsAreStraightenedAlthoughThisCausesWidthIncrease([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  4:1  5:1,4  6:2  7:2,6  8:5  9:5,8,3,7 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsWithOutgoingPrimaryMergesAreStraightened([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  2:1  3:1  6:1  7:1,6  8:3,2  9:7  10:7,9,8 ");

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task SegmentsAreNotStraightenedIfThisCausesAShiftForPrimarySegment([Values] bool mergeGraphLanesHavingCommonParent)
        {
            AppSettings.MergeGraphLanesHavingCommonParent.Value = mergeGraphLanesHavingCommonParent;

            RevisionGraph revisionGraph = CreateGraph(" 1  a:1  b:1  2:1  3:1  4:1  5:4,1  6:3  7:5,6  8:7,2,6  c:8  d:8  e:8  9:8,e,d,c,b,a ");

            // Two segments cross at the 'X'. The one going '/' could be straightened,
            // but then it would shift the parent node causing an unwanted gap.

            await VerifyGraphLayoutAsync(revisionGraph);
        }

        [Test]
        public async Task MoveVisibleAndInvisibleLanesRight([Values] bool moveFirstLane)
        {
            List<GitRevision> laneCommits = [];
            Setup(mergeGraphLanesHavingCommonParent: false, finishLoading: true, GetRevisions().ToArray());

            for (int lane = 0; lane < laneCommits.Count; ++lane)
            {
                GitRevision moveLaneCommit = laneCommits[lane];
                _revisionGraph.TryGetRowIndex(moveLaneCommit.ObjectId, out int lastLaneCommitIndex).Should().BeTrue();
                RevisionGraphRevision revisionGraphRevision = _revisionGraph.GetNodeForRow(lastLaneCommitIndex);
                IRevisionGraphRow? revisionGraphRow = _revisionGraph.GetSegmentsForRow(lastLaneCommitIndex);
                revisionGraphRow.Should().NotBeNull();
                int initialLaneCount = lane + 1;
                revisionGraphRow.GetLaneCount().Should().Be(initialLaneCount);
                revisionGraphRow.MoveLanesRight(moveFirstLane ? 0 : lane);
                revisionGraphRow.GetLaneCount().Should().Be(initialLaneCount + 1);
            }

            await VerifyGraphLayoutAsync(_revisionGraph);

            return;

            IEnumerable<GitRevision> GetRevisions()
            {
                GitRevision baseCommit = new(ObjectId.Random()) { Subject = "B", ParentIds = [] };

                for (int lane = 0; lane < RevisionGraph.MaxLanes + 10; ++lane)
                {
                    GitRevision laneCommit = new(ObjectId.Random()) { Subject = $"{lane % 10}", ParentIds = [baseCommit.ObjectId] };
                    laneCommits.Add(laneCommit);
                    yield return laneCommit;

                    baseCommit.ParentIds.Append(laneCommit.ObjectId);
                }

                yield return baseCommit;
            }
        }

        private const int LookAhead = 20 * 2;

        private static IEnumerable<GitRevision> Revisions
        {
            get
            {
                /* Visualization of commit graph:
                 *     Commit1
                 *        |
                 *     Commit2
                 *    /       \
                 * Commit3     |
                 *   |         |
                 *   |       Commit4
                 *    \       /
                 *     Commit5
                 *        |
                 *     Commit6
                 *        |
                 *       ...
                 *        |
                 *     Commit(6 + LookAhead)
                 */
                GitRevision commit1 = new(ObjectId.Random());

                GitRevision commit2 = new(ObjectId.Random());
                commit1.ParentIds = new ObjectId[] { commit2.ObjectId };

                GitRevision commit3 = new(ObjectId.Random());
                GitRevision commit4 = new(ObjectId.Random());
                commit2.ParentIds = new ObjectId[] { commit3.ObjectId, commit4.ObjectId };

                GitRevision commit5 = new(ObjectId.Random());
                commit3.ParentIds = new ObjectId[] { commit5.ObjectId };
                commit4.ParentIds = new ObjectId[] { commit5.ObjectId };

                yield return commit1;
                yield return commit2;
                yield return commit3;
                yield return commit4;

                GitRevision prevCommit = commit5;
                for (int i = 0; i < 1 + LookAhead; ++i)
                {
                    GitRevision currCommit = new(ObjectId.Random());
                    prevCommit.ParentIds = new ObjectId[] { currCommit.ObjectId };
                    yield return prevCommit;
                    prevCommit = currCommit;
                }

                prevCommit.ParentIds = new ObjectId[] { };
                yield return prevCommit;
            }
        }

        /// <summary>
        /// Creates a revision graph from a text description that lists commits, space separated, from oldest to newest.
        /// Each commit spec is {id}:{parent1 id},{parent2 id},...
        /// or just {id} if it has no parent.
        ///
        /// E.g.: " 1   2:1   3:1   4:2,3 "
        /// </summary>
        private static RevisionGraph CreateGraph(string commitSpecs)
        {
            List<GitRevision> commits = [];
            Dictionary<string, GitRevision> commitsById = [];

            // Parse and create commits
            foreach (string spec in commitSpecs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = spec.Split(':');
                string id = parts[0];

                GitRevision commit = new(ObjectId.Random());
                commit.Subject = id;
                commits.Add(commit);
                commitsById.Add(id, commit);

                if (parts.Length > 1)
                {
                    string[] parentIds = parts[1].Split(',');
                    commit.ParentIds = parentIds.Select(id => commitsById[id].ObjectId).ToList();
                }
            }

            // Add commits to graph from newest to oldest
            RevisionGraph graph = new();
            commits.Reverse();

            foreach (GitRevision commit in commits)
            {
                graph.Add(commit);
            }

            graph.LoadingCompleted();

            int lastRowIndex = graph.Count - 1;
            graph.CacheTo(lastRowIndex, lastRowIndex);

            return graph;
        }

        /// <summary>
        /// Creates an ascii art representation of the <paramref name="revisionGraph"/> with the same layout
        /// as the actual GE GUI.
        /// </summary>
        private static List<string> AsciiGraphFor(RevisionGraph revisionGraph)
        {
            List<string> graph = [];

            int rowIndex = 0;
            while (true)
            {
                // Create a line for commit

                IRevisionGraphRow row = revisionGraph.GetSegmentsForRow(rowIndex);
                char[] line = Enumerable.Repeat(' ', (row.GetLaneCount() * 2) + 1).ToArray();

                // Show '|' in lanes passing through
                foreach (RevisionGraphSegment segment in row.Segments)
                {
                    line[row.GetLaneForSegment(segment).Index * 2] = '|';
                }

                // Show '*' in lane of actual commit
                string? subject = row.Revision.GitRevision.Subject;
                line[row.GetCurrentRevisionLane() * 2] = subject?.Length is 1 ? subject[0] : '*';

                graph.Add(new string(line).TrimEnd());

                IRevisionGraphRow nextRow = revisionGraph.GetSegmentsForRow(rowIndex + 1);
                if (nextRow == null)
                {
                    break;
                }

                // Create a line between commits

                line = Enumerable.Repeat(' ', (Math.Max(row.GetLaneCount(), nextRow.GetLaneCount()) * 2) + 1).ToArray();

                // These drawing actions are done last, to appear on top
                List<Action> actions = [];

                foreach (RevisionGraphSegment segment in row.Segments)
                {
                    int fromPos = row.GetLaneForSegment(segment).Index * 2;
                    int toPos = nextRow.GetLaneForSegment(segment).Index * 2;
                    if (toPos == -2)
                    {
                        // Segment does not continue to next commit
                        continue;
                    }

                    if (toPos == fromPos)
                    {
                        // Segment stays in lane
                        actions.Add(() => line[fromPos] = '|');
                    }
                    else if (toPos == fromPos + 2)
                    {
                        // Segment shifts one lane to the right
                        actions.Add(() =>
                        {
                            if (line[fromPos + 1] == '/')
                            {
                                // , crossing another shifting left
                                line[fromPos + 1] = 'X';
                            }
                            else
                            {
                                line[fromPos + 1] = '\\';
                            }
                        });
                    }
                    else if (toPos == fromPos - 2)
                    {
                        // Segment shifts one lane to the left
                        actions.Add(() =>
                        {
                            if (line[fromPos - 1] == '\\')
                            {
                                // , crossing another shifting right
                                line[fromPos - 1] = 'X';
                            }
                            else
                            {
                                line[fromPos - 1] = '/';
                            }
                        });
                    }
                    else if (toPos > fromPos)
                    {
                        // Segment shifts multiple lanes to the right
                        line[fromPos + 1] = '`';
                        line[toPos] = 'ˎ';
                        for (int pos = fromPos + 2; pos < toPos; ++pos)
                        {
                            line[pos] = '-';
                        }
                    }
                    else if (toPos < fromPos)
                    {
                        // Segment shifts multiple lanes to the left
                        line[fromPos - 1] = '´';
                        line[toPos] = ',';
                        for (int pos = toPos + 1; pos < fromPos - 1; ++pos)
                        {
                            line[pos] = '-';
                        }
                    }
                }

                foreach (Action action in actions)
                {
                    action();
                }

                graph.Add(new string(line).TrimEnd());
                ++rowIndex;
            }

            return graph;
        }

        private static async Task VerifyGraphLayoutAsync(RevisionGraph revisionGraph)
        {
            string actualGraph = AsciiGraphFor(revisionGraph).Join("\n");
            await Verify(actualGraph);
        }
    }
}
