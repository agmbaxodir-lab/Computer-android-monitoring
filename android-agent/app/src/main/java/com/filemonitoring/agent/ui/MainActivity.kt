package com.filemonitoring.agent.ui

import android.Manifest
import android.app.AppOpsManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.os.Process
import android.provider.Settings
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.filemonitoring.agent.R
import com.filemonitoring.agent.api.ApiClient
import com.filemonitoring.agent.db.AppDatabase
import com.filemonitoring.agent.monitor.FileMonitoringManager
import com.filemonitoring.agent.security.SecureStorage
import com.filemonitoring.agent.service.AgentForegroundService
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MainActivity : AppCompatActivity() {

    private lateinit var storage: SecureStorage
    private lateinit var apiClient: ApiClient
    private lateinit var db: AppDatabase
    private lateinit var monitorManager: FileMonitoringManager

    private lateinit var tvDeviceStatus: TextView
    private lateinit var tvDeviceInfo: TextView
    private lateinit var tvQueueInfo: TextView
    private lateinit var tvEventsLog: TextView
    private lateinit var etServerUrl: EditText
    private lateinit var etEnrollmentToken: EditText
    private lateinit var btnEnroll: Button
    private lateinit var btnToggleService: Button
    private lateinit var btnSendTestEvent: Button

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { updateUi() }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        storage = SecureStorage(this)
        apiClient = ApiClient(storage)
        db = AppDatabase.getInstance(this)
        monitorManager = FileMonitoringManager(this)

        initViews()
        setupListeners()
        checkAndRequestPermissions()
        updateUi()
    }

    override fun onResume() {
        super.onResume()
        updateUi()
    }

    private fun initViews() {
        tvDeviceStatus = findViewById(R.id.tvDeviceStatus)
        tvDeviceInfo = findViewById(R.id.tvDeviceInfo)
        tvQueueInfo = findViewById(R.id.tvQueueInfo)
        tvEventsLog = findViewById(R.id.tvEventsLog)
        etServerUrl = findViewById(R.id.etServerUrl)
        etEnrollmentToken = findViewById(R.id.etEnrollmentToken)
        btnEnroll = findViewById(R.id.btnEnroll)
        btnToggleService = findViewById(R.id.btnToggleService)
        btnSendTestEvent = findViewById(R.id.btnSendTestEvent)

        etServerUrl.setText(storage.serverUrl)
    }

    private fun setupListeners() {
        btnEnroll.setOnClickListener {
            val url = etServerUrl.text.toString().trim()
            val token = etEnrollmentToken.text.toString().trim()

            if (url.isEmpty()) {
                Toast.makeText(this, "Iltimos server URL kiriting", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            if (token.isEmpty()) {
                Toast.makeText(this, "Iltimos enrollment token kiriting", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            storage.serverUrl = url
            btnEnroll.isEnabled = false
            btnEnroll.text = "Enrolling..."

            lifecycleScope.launch {
                val success = apiClient.register(enrollmentToken = token)
                btnEnroll.isEnabled = true
                btnEnroll.text = getString(R.string.btn_enroll)

                if (success) {
                    Toast.makeText(this@MainActivity, "Qurilma muvaffaqiyatli ro'yxatdan o'tdi!", Toast.LENGTH_LONG).show()
                    AgentForegroundService.start(this@MainActivity)
                    updateUi()
                } else {
                    Toast.makeText(this@MainActivity, "Ro'yxatdan o'tishda xatolik. Token yoki Server URL'ni tekshiring.", Toast.LENGTH_LONG).show()
                }
            }
        }

        btnToggleService.setOnClickListener {
            if (storage.isServiceEnabled) {
                AgentForegroundService.stop(this)
                storage.isServiceEnabled = false
            } else {
                if (!storage.isRegistered) {
                    Toast.makeText(this, "Avval qurilmani ro'yxatdan o'tkazing", Toast.LENGTH_SHORT).show()
                    return@setOnClickListener
                }
                AgentForegroundService.start(this)
                storage.isServiceEnabled = true
            }
            updateUi()
        }

        btnSendTestEvent.setOnClickListener {
            monitorManager.createTestEvent("confidential_report_${System.currentTimeMillis()}.pdf", "CREATED")
            Toast.makeText(this, "Test event yaratildi va navbatga qo'shildi", Toast.LENGTH_SHORT).show()
            updateUi()
        }

        monitorManager.onEventCaptured = { event ->
            runOnUiThread {
                val line = "[${event.eventType}] ${event.fileName} (${event.fileSize} B, App: ${event.application ?: "Unknown"})\n"
                val cur = tvEventsLog.text.toString().take(1500)
                tvEventsLog.text = "$line$cur"
                updateQueueInfo()
            }
        }
    }

    private fun updateUi() {
        val registered = storage.isRegistered
        val serviceRunning = storage.isServiceEnabled

        if (registered) {
            tvDeviceStatus.text = "Status: Enrolled (${if (serviceRunning) "Monitoring Active" else "Service Stopped"})"
            tvDeviceStatus.setTextColor(ContextCompat.getColor(this, if (serviceRunning) R.color.online_green else R.color.secondary))
            tvDeviceInfo.text = "Device ID: ${storage.deviceId?.take(8)}... | Model: ${Build.MANUFACTURER} ${Build.MODEL} | OS: Android ${Build.VERSION.RELEASE}"
        } else {
            tvDeviceStatus.text = getString(R.string.status_not_registered)
            tvDeviceStatus.setTextColor(ContextCompat.getColor(this, R.color.offline_red))
            tvDeviceInfo.text = "Model: ${Build.MANUFACTURER} ${Build.MODEL} | OS: Android ${Build.VERSION.RELEASE}"
        }

        btnToggleService.text = if (serviceRunning) getString(R.string.btn_stop_service) else getString(R.string.btn_start_service)
        btnToggleService.setBackgroundColor(ContextCompat.getColor(this, if (serviceRunning) R.color.offline_red else R.color.online_green))

        updateQueueInfo()
    }

    private fun updateQueueInfo() {
        lifecycleScope.launch {
            val count = withContext(Dispatchers.IO) { db.eventDao().getCount() }
            val lastHb = storage.lastHeartbeatTime
            val hbStr = if (lastHb > 0) SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date(lastHb)) else "Never"
            tvQueueInfo.text = "Local Queue: $count events | Last Heartbeat: $hbStr"
        }
    }

    private fun checkAndRequestPermissions() {
        val permissions = mutableListOf<String>()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.S_V2) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.READ_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                permissions.add(Manifest.permission.READ_EXTERNAL_STORAGE)
            }
        }

        if (permissions.isNotEmpty()) {
            permissionLauncher.launch(permissions.toTypedArray())
        }

        // Android 11+ MANAGE_EXTERNAL_STORAGE (All Files Access)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            if (!Environment.isExternalStorageManager()) {
                try {
                    val intent = Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION).apply {
                        data = Uri.parse("package:$packageName")
                    }
                    startActivity(intent)
                } catch (e: Exception) {
                    val intent = Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION)
                    startActivity(intent)
                }
            }
        }

        // PACKAGE_USAGE_STATS permission
        val appOps = getSystemService(Context.APP_OPS_SERVICE) as AppOpsManager
        val mode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            appOps.unsafeCheckOpNoThrow(AppOpsManager.OPSTR_GET_USAGE_STATS, Process.myUid(), packageName)
        } else {
            @Suppress("DEPRECATION")
            appOps.checkOpNoThrow(AppOpsManager.OPSTR_GET_USAGE_STATS, Process.myUid(), packageName)
        }
        if (mode != AppOpsManager.MODE_ALLOWED) {
            try {
                val intent = Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS).apply {
                    data = Uri.parse("package:$packageName")
                }
                startActivity(intent)
            } catch (e: Exception) {
                startActivity(Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS))
            }
        }
    }
}
