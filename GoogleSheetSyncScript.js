/**
 * =========================================================================================
 * GOOGLE APPS SCRIPT: ĐỒNG BỘ ĐƠN HÀNG TỪ GOOGLE SHEET VỀ BACKEND THINH PHAT API
 * =========================================================================================
 *
 * HƯỚNG DẪN CÀI ĐẶT TRÊN GOOGLE SHEET:
 * 1. Mở Google Sheet của Sản Phẩm cần đồng bộ.
 * 2. Trên thanh menu, chọn: Tiện ích mở rộng (Extensions) -> Apps Script.
 * 3. Xóa hết code mặc định trong trình chỉnh sửa và dán toàn bộ nội dung file này vào.
 * 4. Điền các giá trị trong khối `CONFIG` bên dưới (đặc biệt là API_URL, API_KEY và PRODUCT_ID).
 * 5. BẤM LƯU (Ctrl + S hoặc biểu tượng Đĩa mềm).
 * 6. (CẤP QUYỀN URLFETCHAPP - BẮT BUỘC):
 *    - Chọn hàm `testConnection` trên thanh công cụ -> Bấm "Chạy" (Run) 1 lần.
 *    - Khi Google hiện popup yêu cầu cấp quyền: Bấm "Xem lại quyền" -> Chọn tài khoản Google
 *      -> Bấm "Nâng cao" (Advanced) -> Chọn "Đi tới script (Không an toàn)" -> Bấm "Cho phép" (Allow).
 * 7. (QUAN TRỌNG VỚI CÁC DÒNG CŨ ĐANG CÓ):
 *    - Chọn hàm `markAllExistingRowsAsSkipped` trên thanh công cụ -> Bấm "Chạy" (Run) 1 lần.
 *    - Toàn bộ các dòng cũ sẽ được đánh dấu "SKIPPED" để không bị gửi trùng về Backend.
 * 8. (KÍCH HOẠT ĐỒNG BỘ TỰ ĐỘNG):
 *    - Chọn hàm `setupTriggers` -> Bấm "Chạy" (Run).
 *    - Xong! Từ nay mỗi khi có dòng mới thêm vào Sheet, script sẽ tự động đẩy sang Backend.
 * =========================================================================================
 */

// ==========================================
// 1. CẤU HÌNH CỐ ĐỊNH CHO TỪNG SHEET SẢN PHẨM
// ==========================================
var CONFIG = {
  // URL API Backend nhận webhook đồng bộ (Thay bằng domain thật của bạn khi deploy production)
  API_URL: "https://thinh-phat-fe.vercel.app/api/v1/sync/google-sheet",

  // Khóa bảo mật kết nối (phải trùng khớp với Sync:GoogleSheetApiKey trong appsettings.json)
  API_KEY: "thinhphat_gsheet_sync_secret_key_2026",

  // GUID của Sản phẩm trong Database tương ứng với Google Sheet này
  PRODUCT_ID: "c4ca59c5-d264-480f-8cbf-fb770810db2c",

  // Vị trí cột trạng thái đồng bộ (Mặc định: Cột E = Cột 5)
  // Cột 1: Thời gian tạo | Cột 2: Tên KH | Cột 3: SĐT | Cột 4: Địa chỉ | Cột 5: Trạng thái Sync
  SYNC_STATUS_COL: 5,

  // Dòng bắt đầu chứa dữ liệu (Bỏ qua dòng 1 là tiêu đề)
  START_ROW: 2,

  // Số lượng sản phẩm mặc định cho mỗi dòng đơn
  DEFAULT_QUANTITY: 1,
};

// ==========================================
// 2. HÀM KIỂM TRA KẾT NỐI & CẤP QUYỀN (CHẠY ĐẦU TIÊN)
// ==========================================
function testConnection() {
  Logger.log("Đang kiểm tra kết nối và kích hoạt cấp quyền...");

  try {
    var testPayload = {
      productId: CONFIG.PRODUCT_ID,
      phoneNumber: "0999999999",
      customerName: "Test Connection",
      address: "Test Address",
      quantity: 1,
    };

    var options = {
      method: "post",
      contentType: "application/json",
      headers: {
        "X-Sync-Key": CONFIG.API_KEY,
      },
      payload: JSON.stringify(testPayload),
      muteHttpExceptions: true,
    };

    var response = UrlFetchApp.fetch(CONFIG.API_URL, options);
    var code = response.getResponseCode();
    var content = response.getContentText();

    Logger.log("Mã phản hồi từ Backend: " + code);
    Logger.log("Nội dung: " + content);

    if (code >= 200 && code < 300) {
      SpreadsheetApp.getUi().alert(
        "✅ KẾT NỐI VÀ CẤP QUYỀN THÀNH CÔNG!\nBackend phản hồi mã: " +
          code +
          "\nBây giờ bạn có thể chạy hàm 'markAllExistingRowsAsSkipped' và 'setupTriggers'.",
      );
    } else {
      SpreadsheetApp.getUi().alert(
        "⚠️ Đã cấp quyền UrlFetchApp thành công nhưng Backend trả về mã lỗi: " +
          code +
          "\nChi tiết: " +
          content +
          "\nVui lòng kiểm tra lại API_URL, API_KEY và PRODUCT_ID trong CONFIG.",
      );
    }
  } catch (err) {
    Logger.log("Lỗi: " + err.message);
    SpreadsheetApp.getUi().alert(
      "❌ LỖI KẾT NỐI: " +
        err.message +
        "\nVui lòng kiểm tra lại URL API có truy cập được từ internet không.",
    );
  }
}

// ==========================================
// 3. HÀM ĐỒNG BỘ CHÍNH (Được gọi tự động bởi Trigger)
// ==========================================
function syncNewRows() {
  var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
  var lastRow = sheet.getLastRow();

  if (lastRow < CONFIG.START_ROW) {
    Logger.log("Sheet chưa có dòng dữ liệu nào.");
    return;
  }

  // Đảm bảo có tiêu đề cột trạng thái ở ô E1
  var headerCell = sheet.getRange(1, CONFIG.SYNC_STATUS_COL);
  if (!headerCell.getValue()) {
    headerCell
      .setValue("Sync_Status")
      .setFontWeight("bold")
      .setBackground("#d9ead3");
  }

  // Đọc toàn bộ dữ liệu từ dòng START_ROW đến lastRow (lấy 5 cột A -> E)
  var numRows = lastRow - CONFIG.START_ROW + 1;
  var dataRange = sheet.getRange(
    CONFIG.START_ROW,
    1,
    numRows,
    CONFIG.SYNC_STATUS_COL,
  );
  var values = dataRange.getValues();

  var syncCount = 0;

  for (var i = 0; i < values.length; i++) {
    var row = values[i];
    var currentRowNumber = CONFIG.START_ROW + i;
    var syncStatus = String(row[CONFIG.SYNC_STATUS_COL - 1] || "").trim();

    // Chỉ xử lý các dòng mà Cột Sync_Status đang trống
    if (syncStatus === "") {
      var rawCreatedAt = row[0]; // Cột 1: Thời gian
      var customerName = String(row[1] || "").trim(); // Cột 2: Tên
      var phoneNumber = String(row[2] || "").trim(); // Cột 3: SĐT
      var address = String(row[3] || "").trim(); // Cột 4: Địa chỉ

      // Chỉ bắt buộc có Số điện thoại (Tên nếu trống backend tự gán "a", Địa chỉ nếu trống để null)
      if (!phoneNumber) {
        continue;
      }

      // Xử lý định dạng ngày tạo (Cột 1)
      var createdAtIso = null;
      if (rawCreatedAt instanceof Date) {
        createdAtIso = rawCreatedAt.toISOString();
      } else if (rawCreatedAt) {
        var strDate = String(rawCreatedAt).trim();
        if (strDate) {
          try {
            var parsed = new Date(strDate);
            if (!isNaN(parsed.getTime())) {
              createdAtIso = parsed.toISOString();
            } else {
              createdAtIso = strDate;
            }
          } catch (e) {
            createdAtIso = strDate;
          }
        }
      }

      // Chuẩn bị payload gửi sang API Backend
      var payload = {
        productId: CONFIG.PRODUCT_ID,
        customerName: customerName ? customerName : null,
        phoneNumber: phoneNumber,
        address: address ? address : null,
        createdAt: createdAtIso,
        quantity: CONFIG.DEFAULT_QUANTITY,
      };

      var options = {
        method: "post",
        contentType: "application/json",
        headers: {
          "X-Sync-Key": CONFIG.API_KEY,
        },
        payload: JSON.stringify(payload),
        muteHttpExceptions: true,
      };

      try {
        var response = UrlFetchApp.fetch(CONFIG.API_URL, options);
        var responseCode = response.getResponseCode();
        var responseText = response.getContentText();

        if (responseCode >= 200 && responseCode < 300) {
          var nowStr = Utilities.formatDate(
            new Date(),
            "GMT+7",
            "yyyy-MM-dd HH:mm:ss",
          );
          sheet
            .getRange(currentRowNumber, CONFIG.SYNC_STATUS_COL)
            .setValue("DONE (" + nowStr + ")")
            .setFontColor("#274e13");
          syncCount++;
          Logger.log("Dòng " + currentRowNumber + ": Đồng bộ thành công!");
        } else {
          sheet
            .getRange(currentRowNumber, CONFIG.SYNC_STATUS_COL)
            .setValue("ERROR " + responseCode)
            .setFontColor("#cc0000");
          Logger.log(
            "Dòng " +
              currentRowNumber +
              " Lỗi (" +
              responseCode +
              "): " +
              responseText,
          );
        }
      } catch (err) {
        sheet
          .getRange(currentRowNumber, CONFIG.SYNC_STATUS_COL)
          .setValue("FAILED: " + err.message)
          .setFontColor("#cc0000");
        Logger.log("Dòng " + currentRowNumber + " Ngoại lệ: " + err.message);
      }
    }
  }

  Logger.log("Đã hoàn tất đồng bộ " + syncCount + " đơn hàng mới.");
}

// ==========================================
// 3. HÀM ĐÁNH DẤU BỎ QUA DỮ LIỆU CŨ (Chạy 1 lần)
// ==========================================
function markAllExistingRowsAsSkipped() {
  var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
  var lastRow = sheet.getLastRow();

  if (lastRow < CONFIG.START_ROW) {
    SpreadsheetApp.getUi().alert(
      "Sheet không có dòng dữ liệu nào để đánh dấu.",
    );
    return;
  }

  // Tạo tiêu đề nếu chưa có
  sheet
    .getRange(1, CONFIG.SYNC_STATUS_COL)
    .setValue("Sync_Status")
    .setFontWeight("bold")
    .setBackground("#d9ead3");

  var numRows = lastRow - CONFIG.START_ROW + 1;
  var statusRange = sheet.getRange(
    CONFIG.START_ROW,
    CONFIG.SYNC_STATUS_COL,
    numRows,
    1,
  );
  var currentStatuses = statusRange.getValues();

  var updatedStatuses = [];
  var markedCount = 0;

  for (var i = 0; i < currentStatuses.length; i++) {
    var val = String(currentStatuses[i][0] || "").trim();
    if (val === "") {
      updatedStatuses.push(["SKIPPED"]);
      markedCount++;
    } else {
      updatedStatuses.push([currentStatuses[i][0]]);
    }
  }

  statusRange.setValues(updatedStatuses).setFontColor("#7f6000");
  Logger.log("Đã đánh dấu SKIPPED cho " + markedCount + " dòng dữ liệu cũ.");
  SpreadsheetApp.getUi().alert(
    "Thành công: Đã đánh dấu 'SKIPPED' cho " +
      markedCount +
      " dòng dữ liệu cũ. Các dòng này sẽ không bị đồng bộ sang Backend.",
  );
}

// ==========================================
// 4. HÀM THIẾT LẬP TRIGGER TỰ ĐỘNG
// ==========================================
function setupTriggers() {
  // Xóa các trigger cũ của hàm syncNewRows để tránh trùng lặp
  var triggers = ScriptApp.getProjectTriggers();
  for (var i = 0; i < triggers.length; i++) {
    if (triggers[i].getHandlerFunction() === "syncNewRows") {
      ScriptApp.deleteTrigger(triggers[i]);
    }
  }

  var ss = SpreadsheetApp.getActiveSpreadsheet();

  // 1. Trigger khi nội dung Sheet thay đổi (onEdit / onChange)
  ScriptApp.newTrigger("syncNewRows").forSpreadsheet(ss).onChange().create();

  // 2. Trigger định kỳ mỗi 5 phút (để quét sót đơn nếu mạng chập chờn)
  ScriptApp.newTrigger("syncNewRows").timeBased().everyMinutes(5).create();

  Logger.log(
    "Đã thiết lập xong 2 Trigger: onChange (Realtime) và 5-Phút Time Trigger.",
  );
  SpreadsheetApp.getUi().alert(
    "Kích hoạt thành công! Hệ thống sẽ tự động đồng bộ khi có dữ liệu mới hoặc định kỳ mỗi 5 phút.",
  );
}
