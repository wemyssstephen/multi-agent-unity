
class TestPollTimeout(Exception):
    """A PlayMode test run exceeded its timeout without resolving"""


class BridgeTimeout(Exception):
    """post() timed out — bridge wedged, abandon the run."""
