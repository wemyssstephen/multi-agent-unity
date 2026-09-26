
class TestPollTimeout(Exception):
    """A PlayMode test run exceeded its timeout without resolving"""

class BridgeTimeout(Exception):
    """post() timed out — bridge wedged, abandon the run."""

class BridgeUnreachable(BridgeTimeout):
    """The bridge refused connections for every retry: Unity has probably crashed."""

class UnityStartupError(Exception):
    """A freshly launched Unity never answered on the bridge."""
