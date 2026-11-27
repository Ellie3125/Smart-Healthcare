import { beforeEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../contexts/AuthContext';
import { RegisterPage } from '../pages/RegisterPage';
import { patientApiMock } from './patientTestFixtures';

function renderRegisterPage() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/register']}>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/booking" element={<div>Booking Page</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe('RegisterPage inline onBlur validation', () => {
  beforeEach(() => {
    patientApiMock.reset();
    localStorage.clear();
  });

  it('shows error right below field when blurring empty or invalid account inputs', () => {
    renderRegisterPage();

    const usernameInput = screen.getByLabelText(/Tên đăng nhập/i);
    const emailInput = screen.getByLabelText(/Địa chỉ Email/i);
    const phoneInput = screen.getByLabelText(/Số điện thoại liên hệ/i);
    const passwordInput = screen.getByLabelText(/^Mật khẩu/i);
    const confirmInput = screen.getByLabelText(/Xác nhận lại mật khẩu/i);

    // Leave username empty and click email
    fireEvent.focus(usernameInput);
    fireEvent.blur(usernameInput);
    expect(screen.getByText('Tên đăng nhập không được để trống.')).toBeInTheDocument();
    expect(usernameInput).toHaveAttribute('aria-invalid', 'true');
    expect(usernameInput).toHaveAttribute('aria-describedby', 'reg-username-error');

    // Type short username -> blur
    fireEvent.change(usernameInput, { target: { value: 'ab' } });
    expect(screen.getByText('Tên đăng nhập phải có ít nhất 3 ký tự.')).toBeInTheDocument();

    // Fix username -> error clears
    fireEvent.change(usernameInput, { target: { value: 'nguyenvanan' } });
    expect(document.getElementById('reg-username-error')).not.toBeInTheDocument();

    // Invalid email on blur
    fireEvent.change(emailInput, { target: { value: 'invalid-email' } });
    fireEvent.blur(emailInput);
    expect(screen.getByText('Định dạng email không hợp lệ.')).toBeInTheDocument();

    // Invalid phone on blur
    fireEvent.change(phoneInput, { target: { value: '12345' } });
    fireEvent.blur(phoneInput);
    expect(screen.getByText(/Số điện thoại không đúng định dạng Việt Nam/i)).toBeInTheDocument();

    // Empty password on blur
    fireEvent.focus(passwordInput);
    fireEvent.blur(passwordInput);
    expect(screen.getByText('Mật khẩu không được để trống.')).toBeInTheDocument();

    // Short password
    fireEvent.change(passwordInput, { target: { value: '123' } });
    expect(screen.getByText('Mật khẩu phải có ít nhất 6 ký tự.')).toBeInTheDocument();

    // Valid password, mismatched confirm password
    fireEvent.change(passwordInput, { target: { value: '123456' } });
    fireEvent.change(confirmInput, { target: { value: '654321' } });
    fireEvent.blur(confirmInput);
    expect(screen.getByText('Mật khẩu xác nhận không khớp.')).toBeInTheDocument();

    // Changing password to match clears confirm error
    fireEvent.change(passwordInput, { target: { value: '654321' } });
    expect(screen.queryByText('Mật khẩu xác nhận không khớp.')).not.toBeInTheDocument();
  });

  it('shows errors right below self-profile fields (including dateOfBirth) on blur', () => {
    renderRegisterPage();

    const fullNameInput = screen.getByLabelText(/Họ và tên đầy đủ/i);
    const dobInput = screen.getByLabelText(/Ngày sinh/i);
    const cccdInput = screen.getByLabelText(/Số CCCD \/ Định danh/i);
    const selfPhoneInput = screen.getByLabelText(/Số điện thoại hồ sơ/i);
    const addressInput = screen.getByLabelText(/Địa chỉ liên hệ/i);

    fireEvent.blur(fullNameInput);
    expect(screen.getByText('Họ và tên không được để trống.')).toBeInTheDocument();

    // Empty date of birth on blur
    fireEvent.blur(dobInput);
    expect(screen.getByText('Ngày sinh không được để trống.')).toBeInTheDocument();

    // Future date of birth
    fireEvent.change(dobInput, { target: { value: '2099-01-01' } });
    fireEvent.blur(dobInput);
    expect(screen.getByText('Ngày sinh không thể ở tương lai.')).toBeInTheDocument();

    // > 120 years old
    fireEvent.change(dobInput, { target: { value: '1850-01-01' } });
    fireEvent.blur(dobInput);
    expect(screen.getByText('Ngày sinh không được quá 120 tuổi.')).toBeInTheDocument();

    // Valid date of birth clears error
    fireEvent.change(dobInput, { target: { value: '1995-05-20' } });
    expect(document.getElementById('self-dob-error')).not.toBeInTheDocument();

    // Invalid CCCD
    fireEvent.change(cccdInput, { target: { value: '12345' } });
    fireEvent.blur(cccdInput);
    expect(screen.getByText('Số CCCD/định danh cá nhân phải gồm đúng 12 chữ số.')).toBeInTheDocument();

    // Empty self phone & address
    fireEvent.blur(selfPhoneInput);
    expect(screen.getByText('Số điện thoại liên hệ của hồ sơ bệnh nhân không được để trống.')).toBeInTheDocument();

    fireEvent.blur(addressInput);
    expect(screen.getByText('Địa chỉ thường trú không được để trống.')).toBeInTheDocument();
  });

  it('requires at least email or phone when both are blurred empty and clears when one is entered', () => {
    renderRegisterPage();

    const emailInput = screen.getByLabelText(/Địa chỉ Email/i);
    const phoneInput = screen.getByLabelText(/Số điện thoại liên hệ/i);

    // Leaving email empty alone does not error before phone is visited
    fireEvent.blur(emailInput);
    expect(screen.queryByText(/Vui lòng cung cấp email hoặc số điện thoại/i)).not.toBeInTheDocument();

    // Leaving phone empty too shows missing contact error
    fireEvent.blur(phoneInput);
    expect(screen.getAllByText('Vui lòng cung cấp email hoặc số điện thoại để đăng ký.')).toHaveLength(2);

    // Filling email clears the missing contact error
    fireEvent.change(emailInput, { target: { value: 'user@example.com' } });
    expect(screen.queryByText('Vui lòng cung cấp email hoặc số điện thoại để đăng ký.')).not.toBeInTheDocument();
  });

  it('clears self-profile errors when unchecking createSelf checkbox', () => {
    renderRegisterPage();

    const fullNameInput = screen.getByLabelText(/Họ và tên đầy đủ/i);
    fireEvent.blur(fullNameInput);
    expect(screen.getByText('Họ và tên không được để trống.')).toBeInTheDocument();

    const toggle = screen.getByLabelText(/Tạo hồ sơ ngay/i);
    fireEvent.click(toggle);
    expect(screen.queryByText('Họ và tên không được để trống.')).not.toBeInTheDocument();
  });
});
