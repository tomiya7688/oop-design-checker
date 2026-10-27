import unittest
from unittest.mock import Mock

try:
    from selenium.common.exceptions import (
        NoSuchElementException,
        StaleElementReferenceException,
        WebDriverException,
    )
    from run_appium import 要素が表示される
except ModuleNotFoundError as error:
    if error.name not in ("appium", "selenium"):
        raise
    要素が表示される = None


@unittest.skipUnless(要素が表示される, "Appium test dependencies are not installed")
class 要素表示判定テスト(unittest.TestCase):
    def test_要素不在と古い参照は非表示を返す(self):
        for exception in (NoSuchElementException(), StaleElementReferenceException()):
            with self.subTest(exception=type(exception).__name__):
                driver = Mock()
                driver.find_element.side_effect = exception
                self.assertFalse(要素が表示される(driver, "TargetError"))

    def test_接続エラーは呼び出し元へ伝える(self):
        driver = Mock()
        driver.find_element.side_effect = WebDriverException("broken session")
        with self.assertRaises(WebDriverException):
            要素が表示される(driver, "TargetError")

    def test_表示中の要素は真を返す(self):
        driver = Mock()
        driver.find_element.return_value.is_displayed.return_value = True
        self.assertTrue(要素が表示される(driver, "TargetError"))


if __name__ == "__main__":
    unittest.main()
