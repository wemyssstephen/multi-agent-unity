import logging


def setup_logging(console_level=logging.INFO,  file_level=logging.DEBUG, logfile: str | None = None):
    root = logging.getLogger()
    root.setLevel(logging.DEBUG)
    root.handlers.clear()

    format = logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")

    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(format)
    root.addHandler(console_handler)

    if logfile:
        file_handler = logging.FileHandler(logfile)
        file_handler.setLevel(file_level)
        file_handler.setFormatter(format)
        root.addHandler(file_handler)
        
    logging.getLogger("urllib3").setLevel(logging.WARNING)
    logging.getLogger("charset_normalizer").setLevel(logging.WARNING)
